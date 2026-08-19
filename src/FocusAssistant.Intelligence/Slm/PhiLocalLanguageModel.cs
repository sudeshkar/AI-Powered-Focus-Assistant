using FocusAssistant.Core.Intelligence;
using FocusAssistant.Intelligence.Prompting;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntimeGenAI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Intelligence.Slm
{
    /// <summary>
    /// Phi-3.5-mini running locally through ONNX Runtime GenAI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loaded lazily on first use and unloaded again after a period of silence. Both halves
    /// matter: loading takes seconds, so it cannot be on the startup path, and the loaded
    /// model holds around 2.5GB, which is not something to leave resident in a tray app
    /// somebody keeps running all day. A background process quietly holding 2.5GB is how an
    /// optional feature turns into a reason to uninstall.
    /// </para>
    /// <para>
    /// One generation runs at a time. The model is not thread-safe, and running two
    /// inferences at once on a CPU build would make both slower than running them in turn.
    /// </para>
    /// <para>
    /// Nothing here throws at callers. Every failure - missing files, a load error, a
    /// timeout - becomes null, because the features above this are all designed to work
    /// without a model and must not acquire error handling just because one is present.
    /// </para>
    /// </remarks>
    public sealed class PhiLocalLanguageModel : ILocalLanguageModel, IDisposable
    {
        /// <summary>
        /// Ceiling on a single generation. On CPU INT4 this model runs at roughly 5-20
        /// tokens a second, so a request that has not finished inside this has gone wrong -
        /// and no output in this app is worth blocking on for longer.
        /// </summary>
        private static readonly TimeSpan GenerationTimeout = TimeSpan.FromSeconds(45);

        private readonly IModelProvisioner _provisioner;
        private readonly ILogger<PhiLocalLanguageModel> _logger;
        private readonly string _modelDirectory;
        private readonly TimeSpan _idleUnloadAfter;

        private readonly SemaphoreSlim _gate = new(1, 1);

        private Model? _model;
        private Tokenizer? _tokenizer;
        private DateTime _lastUsedUtc = DateTime.UtcNow;
        private Timer? _idleTimer;
        private ModelAvailability _availability = ModelAvailability.NotDownloaded;
        private bool _disposed;

        public event EventHandler<ModelAvailability>? AvailabilityChanged;

        public ModelAvailability Availability
        {
            get => _availability;
            private set
            {
                if (_availability == value)
                    return;

                _availability = value;
                AvailabilityChanged?.Invoke(this, value);
            }
        }

        public PhiLocalLanguageModel(
            IModelProvisioner provisioner,
            ILogger<PhiLocalLanguageModel> logger,
            string modelDirectory,
            TimeSpan idleUnloadAfter)
        {
            _provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelDirectory = modelDirectory ?? throw new ArgumentNullException(nameof(modelDirectory));
            _idleUnloadAfter = idleUnloadAfter;

            // Availability describes what this object can do right now, and at construction
            // that is nothing either way: files on disk are not a loaded model, and loading
            // only happens on first use. The provisioner is the thing to ask whether the
            // download exists.
            Availability = ModelAvailability.NotDownloaded;
        }

        public async Task<string?> GenerateAsync(LlmRequest request, CancellationToken ct = default)
        {
            var builder = new StringBuilder();
            await foreach (var chunk in StreamAsync(request, ct).ConfigureAwait(false))
                builder.Append(chunk);

            var text = PhiPromptFormatter.Clean(builder.ToString());
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        public async IAsyncEnumerable<string> StreamAsync(
            LlmRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (!_provisioner.IsDownloaded)
                yield break;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(GenerationTimeout);

            if (!await _gate.WaitAsync(TimeSpan.FromSeconds(30), timeout.Token).ConfigureAwait(false))
            {
                _logger.LogWarning("Timed out waiting for the model to become free");
                yield break;
            }

            try
            {
                if (!await EnsureLoadedAsync().ConfigureAwait(false))
                    yield break;

                _lastUsedUtc = DateTime.UtcNow;

                foreach (var token in Generate(request, timeout.Token))
                    yield return token;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// The generation loop. Separate from the async iterator because ORT GenAI's API is
        /// synchronous and cannot be awaited between tokens.
        /// </summary>
        private IEnumerable<string> Generate(LlmRequest request, CancellationToken ct)
        {
            var prompt = PhiPromptFormatter.Format(request);

            using var sequences = _tokenizer!.Encode(prompt);
            using var generatorParams = new GeneratorParams(_model!);

            generatorParams.SetSearchOption("max_length", sequences[0].Length + request.MaxNewTokens);
            generatorParams.SetSearchOption("temperature", request.Temperature);
            generatorParams.SetSearchOption("do_sample", request.Temperature > 0.01f);

            using var generator = new Generator(_model!, generatorParams);
            generator.AppendTokenSequences(sequences);

            using var stream = _tokenizer!.CreateStream();

            while (!generator.IsDone())
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Generation cancelled or timed out");
                    yield break;
                }

                generator.GenerateNextToken();

                var sequence = generator.GetSequence(0);
                var decoded = stream.Decode(sequence[^1]);

                if (!string.IsNullOrEmpty(decoded))
                    yield return decoded;
            }
        }

        private async Task<bool> EnsureLoadedAsync()
        {
            if (_model is not null)
                return true;

            try
            {
                Availability = ModelAvailability.Loading;
                var sw = Stopwatch.StartNew();

                // Loading blocks for seconds and ORT GenAI has no async entry point, so it
                // goes to the thread pool rather than onto whichever thread asked.
                await Task.Run(() =>
                {
                    // The downloaded genai_config.json sets no thread count, which leaves
                    // ONNX Runtime's default: use every logical core for intra-op
                    // parallelism. For a background feature that is the wrong default - a
                    // generation call would peg every core for its ~15 second duration and
                    // starve the UI thread's message pump of CPU time, which is what "the
                    // app feels unresponsive" actually was on this machine (confirmed via
                    // Task Manager during a generation: full CPU saturation, 40+ threads).
                    // Config.Overlay applies this in memory over the downloaded config
                    // without touching the file on disk, so it never has to be reconciled
                    // against the download manifest's recorded byte sizes.
                    using var config = new Config(_modelDirectory);
                    var reservedForUi = 2;
                    var threads = Math.Max(1, Environment.ProcessorCount - reservedForUi);
                    config.Overlay($$"""
                        {
                            "model": {
                                "decoder": {
                                    "session_options": {
                                        "intra_op_num_threads": {{threads}},
                                        "inter_op_num_threads": 1
                                    }
                                }
                            }
                        }
                        """);

                    _model = new Model(config);
                    _tokenizer = new Tokenizer(_model);
                }).ConfigureAwait(false);

                sw.Stop();
                _logger.LogInformation("Local language model loaded in {Elapsed} ms", sw.ElapsedMilliseconds);

                StartIdleTimer();
                Availability = ModelAvailability.Ready;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not load the local language model");
                Availability = ModelAvailability.Failed;
                _model = null;
                _tokenizer = null;
                return false;
            }
        }

        private void StartIdleTimer()
        {
            _idleTimer?.Dispose();
            _idleTimer = new Timer(_ => UnloadIfIdle(), null, _idleUnloadAfter, _idleUnloadAfter);
        }

        private void UnloadIfIdle()
        {
            // Never block the timer thread waiting for a generation to finish; if the model
            // is busy it is by definition not idle.
            if (!_gate.Wait(0))
                return;

            try
            {
                if (_model is null || DateTime.UtcNow - _lastUsedUtc < _idleUnloadAfter)
                    return;

                UnloadCore();
                _logger.LogInformation("Local language model unloaded after {Minutes:F0} minutes idle",
                    _idleUnloadAfter.TotalMinutes);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Caller must hold the gate.</summary>
        private void UnloadCore()
        {
            _tokenizer?.Dispose();
            _model?.Dispose();
            _tokenizer = null;
            _model = null;

            _idleTimer?.Dispose();
            _idleTimer = null;

            Availability = ModelAvailability.NotDownloaded;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _gate.Wait(TimeSpan.FromSeconds(5));
            try
            {
                UnloadCore();
            }
            finally
            {
                _gate.Release();
                _gate.Dispose();
            }
        }
    }
}
