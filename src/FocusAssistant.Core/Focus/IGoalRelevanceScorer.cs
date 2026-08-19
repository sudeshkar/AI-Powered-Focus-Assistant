using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Focus
{
    /// <summary>
    /// Scores activity against what the user said they sat down to do.
    /// </summary>
    /// <remarks>
    /// This is what lets a nudge be specific. "You are distracted" is a guess about
    /// someone; "25 minutes in YouTube since you started 'finish the API docs'" is an
    /// observation they can argue with.
    /// </remarks>
    public interface IGoalRelevanceScorer
    {
        Task SetGoalAsync(string? goal, CancellationToken ct = default);

        /// <summary>0-1 similarity to the current goal; null when no goal is set.</summary>
        ValueTask<double?> ScoreAsync(ActivityContext context, CancellationToken ct = default);
    }
}
