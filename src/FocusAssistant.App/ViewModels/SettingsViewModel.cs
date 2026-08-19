using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusAssistant.Core.Intelligence;
using FocusAssistant.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.ViewModels
{
    /// <summary>
    /// Settings, currently the on-device AI section.
    /// </summary>
    /// <remarks>
    /// The download is opt-in and stays that way. Nothing here starts on its own: a 2.59GB
    /// transfer is the user's decision, possibly on someone's phone tethering, and an app
    /// that helps you focus has no business quietly saturating your connection to do it.
    /// The copy says what it costs and what it buys, because the honest version of this
    /// choice is the one people can actually make.
    /// </remarks>
    public sealed partial class SettingsViewModel : ObservableObject
    {
        private readonly IModelProvisioner _provisioner;
        private readonly ILocalLanguageModel _model;
        private readonly ILogger<SettingsViewModel> _logger;

        private CancellationTokenSource? _downloadCancellation;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDownload))]
        [NotifyPropertyChangedFor(nameof(CanDelete))]
        private bool _isDownloaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDownload))]
        [NotifyPropertyChangedFor(nameof(CanCancel))]
        [NotifyPropertyChangedFor(nameof(CanDelete))]
        private bool _isDownloading;

        [ObservableProperty]
        private double _progressPercent;

        [ObservableProperty]
        private string _statusText = "Checking...";

        [ObservableProperty]
        private string _sizeText = string.Empty;

        [ObservableProperty]
        private string _diskText = string.Empty;

        /// <summary>Result of the "Test model" button, so the user can see it actually works.</summary>
        [ObservableProperty]
        private string? _testOutput;

        [ObservableProperty]
        private bool _isTesting;

        public bool CanDownload => !IsDownloaded && !IsDownloading;
        public bool CanCancel => IsDownloading;
        public bool CanDelete => IsDownloaded && !IsDownloading;

        public SettingsViewModel(
            IModelProvisioner provisioner,
            ILocalLanguageModel model,
            ILogger<SettingsViewModel> logger)
        {
            _provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Refresh()
        {
            IsDownloaded = _provisioner.IsDownloaded;
            SizeText = $"{_provisioner.EstimatedBytes / (1024.0 * 1024 * 1024):F2} GB";
            StatusText = IsDownloaded ? "Installed and ready" : "Not installed";
            ProgressPercent = IsDownloaded ? 100 : 0;

            try
            {
                var root = Path.GetPathRoot(AppPaths.ModelDirectory);
                if (!string.IsNullOrEmpty(root))
                    DiskText = $"{new DriveInfo(root).AvailableFreeSpace / (1024.0 * 1024 * 1024):F0} GB free on {root}";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read free disk space");
                DiskText = string.Empty;
            }
        }

        [RelayCommand]
        private async Task DownloadAsync()
        {
            if (!CanDownload)
                return;

            _downloadCancellation = new CancellationTokenSource();
            IsDownloading = true;
            TestOutput = null;

            var progress = new Progress<ModelDownloadProgress>(p =>
            {
                ProgressPercent = p.Fraction * 100;
                StatusText =
                    $"Downloading {p.BytesReceived / (1024.0 * 1024 * 1024):F2} of " +
                    $"{p.BytesTotal / (1024.0 * 1024 * 1024):F2} GB  ·  file {p.FileIndex} of {p.FileCount}";
            });

            try
            {
                var ok = await _provisioner.EnsureDownloadedAsync(progress, _downloadCancellation.Token);
                StatusText = ok ? "Installed and ready" : "Download failed - you can try again";
            }
            catch (OperationCanceledException)
            {
                // The partial files stay on disk, so resuming later picks up where this left
                // off rather than starting the 2.59GB again.
                StatusText = "Paused - resumes from here if you download again";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model download failed");
                StatusText = $"Download failed: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                _downloadCancellation?.Dispose();
                _downloadCancellation = null;
                Refresh();
            }
        }

        [RelayCommand]
        private void Cancel() => _downloadCancellation?.Cancel();

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (!CanDelete)
                return;

            await _provisioner.DeleteAsync();
            TestOutput = null;
            Refresh();
        }

        /// <summary>
        /// Runs one real generation, so "installed" can be seen to mean "working".
        /// </summary>
        [RelayCommand]
        private async Task TestAsync()
        {
            if (!IsDownloaded || IsTesting)
                return;

            IsTesting = true;
            TestOutput = "Loading the model - this takes a few seconds the first time...";

            try
            {
                var reply = await _model.GenerateAsync(new LlmRequest(
                    System: "You are a concise writing assistant inside a focus app.",
                    User: "In one short sentence, say that you are running locally on this computer.",
                    MaxNewTokens: 60));

                TestOutput = reply ?? "The model did not produce a response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model test failed");
                TestOutput = $"Test failed: {ex.Message}";
            }
            finally
            {
                IsTesting = false;
            }
        }
    }
}
