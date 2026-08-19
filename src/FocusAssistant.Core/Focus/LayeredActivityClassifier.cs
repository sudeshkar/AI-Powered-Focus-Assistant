using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Composes the classification layers, most authoritative first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is: an explicit user correction, then an explicit keyword rule, then the
    /// embedding model, then similarity to the session goal, then a flagged guess. Each
    /// layer may decline, and the first one willing to commit wins.
    /// </para>
    /// <para>
    /// The rules deliberately sit <i>above</i> the model. A keyword hit on "devenv.exe" is
    /// certain in a way a cosine similarity never is, and running a model to second-guess
    /// it would be slower and worse. The model's job is the case the ruleset used to
    /// paper over: an application it has never heard of, which it silently assumed was
    /// productive.
    /// </para>
    /// <para>
    /// This type has no ONNX, no WPF and no EF in it, which is the point - the precedence
    /// rules are the part most worth testing, and they can be tested with fakes.
    /// </para>
    /// </remarks>
    public sealed class LayeredActivityClassifier : IActivityClassifier
    {
        /// <summary>
        /// Window titles change constantly, so this is keyed on app+title and must be
        /// bounded. Same idiom and same rationale as the ruleset's own cache: once full,
        /// stop caching rather than evicting, because eviction needs a lock and a miss
        /// only costs a re-run.
        /// </summary>
        private const int MaxCacheEntries = 2_000;

        private readonly IRuleMatcher _ruleMatcher;
        private readonly ISemanticClassifier _semanticClassifier;
        private readonly IGoalRelevanceScorer _goalScorer;
        private readonly IUserOverrideStore _overrides;

        private readonly ConcurrentDictionary<string, ProductivityVerdict> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public LayeredActivityClassifier(
            IRuleMatcher ruleMatcher,
            ISemanticClassifier semanticClassifier,
            IGoalRelevanceScorer goalScorer,
            IUserOverrideStore overrides)
        {
            _ruleMatcher = ruleMatcher ?? throw new ArgumentNullException(nameof(ruleMatcher));
            _semanticClassifier = semanticClassifier ?? throw new ArgumentNullException(nameof(semanticClassifier));
            _goalScorer = goalScorer ?? throw new ArgumentNullException(nameof(goalScorer));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
        }

        public ProductivityVerdict ClassifyFast(ActivityContext context)
        {
            if (TryClassifyAuthoritatively(context, out var verdict))
                return verdict;

            // The model cannot run here, but it may already have answered this exact
            // app+title before - and it usually has, because people return to the same
            // handful of windows all day.
            if (_cache.TryGetValue(CacheKey(context), out var cached))
                return cached;

            // Nothing better is available synchronously, so a weak answer beats none.
            if (TryClassifyAdvisory(context, out var advisory))
                return advisory;

            return ProductivityVerdict.Guess();
        }

        public async ValueTask<ProductivityVerdict> ClassifyAsync(
            ActivityContext context, CancellationToken ct = default)
        {
            if (TryClassifyAuthoritatively(context, out var verdict))
                return verdict;

            var key = CacheKey(context);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var semantic = await _semanticClassifier.ClassifyAsync(context, ct).ConfigureAwait(false);
            if (semantic is { } fromModel)
            {
                Cache(key, fromModel);
                return fromModel;
            }

            // The model abstained. If the user named a goal, similarity to it is a weaker
            // but still real signal - and it is the only layer that knows what this
            // particular session is for.
            var relevance = await _goalScorer.ScoreAsync(context, ct).ConfigureAwait(false);
            if (relevance is { } score && Math.Abs(score - 0.5) >= 0.15)
            {
                var goalVerdict = new ProductivityVerdict(
                    IsProductive: score >= 0.5,
                    Confidence: Math.Clamp(Math.Abs(score - 0.5) * 2, 0, 1),
                    Source: ClassificationSource.GoalRelevance,
                    Rationale: score >= 0.5
                        ? "related to this session's goal"
                        : "unrelated to this session's goal");

                Cache(key, goalVerdict);
                return goalVerdict;
            }

            // The model abstained and no goal was set, so the ruleset's weak opinion about
            // ambiguous applications is the last thing left worth saying.
            if (TryClassifyAdvisory(context, out var advisory))
                return advisory;

            // Nothing knew. Do not cache a guess: the model may simply not have finished
            // loading yet, and caching now would make that permanent for this app+title.
            return ProductivityVerdict.Guess();
        }

        /// <summary>
        /// Applications whose name says nothing about what the user is doing in them.
        /// </summary>
        /// <remarks>
        /// A browser is the whole internet behind one process name: a code review and an
        /// infinite video feed are the same executable. The ruleset's answer for these -
        /// distracting, unless the title happens to contain a work keyword - is the weakest
        /// signal in the system, and it sits in front of the layer that reads titles
        /// properly. So for these categories the rule does not short-circuit; it becomes
        /// the fallback for when the model has nothing to say. Every other rule match is
        /// still authoritative, because "devenv.exe" really does settle the question.
        /// </remarks>
        private static readonly string[] AmbiguousCategories = ["Web Browser"];

        /// <summary>
        /// Layers that settle the question outright: an explicit user correction, or a rule
        /// about an application whose identity is meaningful. No I/O and no awaiting, so
        /// this is safe on the hot path.
        /// </summary>
        private bool TryClassifyAuthoritatively(ActivityContext context, out ProductivityVerdict verdict)
        {
            if (_overrides.Match(context.AppName, context.WindowTitle) is { } overridden)
            {
                verdict = new ProductivityVerdict(overridden, 1.0, ClassificationSource.UserOverride,
                    "you told me this one");
                return true;
            }

            var match = _ruleMatcher.Match(context.AppName, context.WindowTitle);
            if (IsAmbiguous(match))
            {
                verdict = default;
                return false;
            }

            switch (match.Kind)
            {
                case RuleMatchKind.ExplicitProductive:
                    verdict = new ProductivityVerdict(true, 0.9, ClassificationSource.Rule,
                        $"a known {match.Category} application");
                    return true;

                case RuleMatchKind.ExplicitDistracting:
                    verdict = new ProductivityVerdict(false, 0.9, ClassificationSource.Rule,
                        $"a known {match.Category} application");
                    return true;

                default:
                    verdict = default;
                    return false;
            }
        }

        /// <summary>
        /// The ruleset's opinion about an ambiguous application - used only once the model
        /// has declined to answer.
        /// </summary>
        private bool TryClassifyAdvisory(ActivityContext context, out ProductivityVerdict verdict)
        {
            var match = _ruleMatcher.Match(context.AppName, context.WindowTitle);
            if (!IsAmbiguous(match))
            {
                verdict = default;
                return false;
            }

            // Deliberately low confidence, which keeps the intervention policy from acting
            // on it: "a work keyword appeared in a browser title" is a guess with better
            // manners, not knowledge.
            verdict = match.Kind == RuleMatchKind.TitleRescued
                ? new ProductivityVerdict(true, 0.4, ClassificationSource.Rule,
                    "the window title looks work-related")
                : new ProductivityVerdict(false, 0.4, ClassificationSource.Rule,
                    $"an unrecognised page in {match.Category}");
            return true;
        }

        private static bool IsAmbiguous(RuleMatch match) =>
            match.Kind is RuleMatchKind.ExplicitDistracting or RuleMatchKind.TitleRescued
            && System.Array.IndexOf(AmbiguousCategories, match.Category) >= 0;

        private void Cache(string key, ProductivityVerdict verdict)
        {
            if (_cache.Count < MaxCacheEntries)
                _cache.TryAdd(key, verdict);
        }

        private static string CacheKey(ActivityContext context) =>
            $"{context.AppName}{context.WindowTitle}";
    }
}
