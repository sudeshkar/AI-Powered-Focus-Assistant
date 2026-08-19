using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusAssistant.Appearance;
using FocusAssistant.Core.Intelligence;
using FocusAssistant.Configuration;
using FocusAssistant.Hosting;
using FocusAssistant.Privacy;
using Microsoft.Extensions.Options;
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
        private readonly PauseController _pauseController;
        private readonly DataManagementService _dataManagement;
        private readonly IOptionsMonitor<PrivacyOptions> _privacyOptions;
        private readonly ThemeService _themeService;
        private readonly ILogger<SettingsViewModel> _logger;

        private CancellationTokenSource? _downloadCancellation;

        [ObservableProperty]
        private AppThemePreference _selectedTheme;

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

        [ObservableProperty]
        private bool _isPaused;

        [ObservableProperty]
        private string _pauseStatusText = string.Empty;

        [ObservableProperty]
        private string _retentionSummary = string.Empty;

        [ObservableProperty]
        private string _titleCaptureSummary = string.Empty;

        [ObservableProperty]
        private string _excludedProcessesSummary = string.Empty;

        [ObservableProperty]
        private string? _dataActionStatus;

        /// <summary>
        /// Requires one click to arm and a second to confirm, rather than a Yes/No dialog -
        /// deleting everything is exactly the kind of action that should not be dismissible
        /// with the same reflexive click that closes an unrelated popup.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DeleteButtonLabel))]
        private bool _deleteArmed;

        public string DeleteButtonLabel => DeleteArmed ? "Click again to confirm" : "Delete all tracked activity";

        public bool CanDownload => !IsDownloaded && !IsDownloading;
        public bool CanCancel => IsDownloading;
        public bool CanDelete => IsDownloaded && !IsDownloading;

        public SettingsViewModel(
            IModelProvisioner provisioner,
            ILocalLanguageModel model,
            PauseController pauseController,
            DataManagementService dataManagement,
            IOptionsMonitor<PrivacyOptions> privacyOptions,
            ThemeService themeService,
            ILogger<SettingsViewModel> logger)
        {
            _provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _pauseController = pauseController ?? throw new ArgumentNullException(nameof(pauseController));
            _dataManagement = dataManagement ?? throw new ArgumentNullException(nameof(dataManagement));
            _privacyOptions = privacyOptions ?? throw new ArgumentNullException(nameof(privacyOptions));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _pauseController.PropertyChanged += (_, _) => RefreshPauseState();

            // Read once, from whatever MainWindow already applied at startup, rather than
            // both places loading the store independently and risking the two disagreeing.
            _selectedTheme = _themeService.Current;
        }

        public void Refresh()
        {
            RefreshPauseState();

            var privacy = _privacyOptions.CurrentValue;
            RetentionSummary = privacy.RetentionDays > 0
                ? $"Per-app activity detail is kept for {privacy.RetentionDays} days, then removed. Daily totals are kept."
                : "Activity detail is kept indefinitely.";
            TitleCaptureSummary = privacy.TitleCapture switch
            {
                Core.Privacy.TitleCaptureMode.Full => "Window titles are recorded in full.",
                Core.Privacy.TitleCaptureMode.AppOnly => "Only application names are recorded - window titles are never stored.",
                Core.Privacy.TitleCaptureMode.Redacted => "Only the activity's category is recorded, never the window title text.",
                _ => string.Empty,
            };
            ExcludedProcessesSummary = privacy.ExcludedProcesses.Length > 0
                ? $"Never tracked in detail: {string.Join(", ", privacy.ExcludedProcesses)}."
                : "No applications are excluded from tracking.";

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

        private void RefreshPauseState()
        {
            IsPaused = _pauseController.IsPaused;
            PauseStatusText = _pauseController switch
            {
                { IsPaused: true, ResumesAt: { } resumesAt } => $"Paused until {resumesAt.LocalDateTime:t}",
                { IsPaused: true } => "Paused",
                _ => "Tracking is active",
            };
        }

        [RelayCommand]
        private Task Pause30MinutesAsync() => _pauseController.PauseAsync(TimeSpan.FromMinutes(30));

        [RelayCommand]
        private Task Pause2HoursAsync() => _pauseController.PauseAsync(TimeSpan.FromHours(2));

        [RelayCommand]
        private Task PauseUntilTomorrowAsync() => _pauseController.PauseUntilTomorrowAsync();

        [RelayCommand]
        private Task ResumeTrackingAsync() => _pauseController.ResumeAsync();

        /// <summary>Parses from a XAML CommandParameter string rather than binding directly
        /// to the enum, which would need the CLR namespace declared and an x:Static
        /// reference on every button for a three-way choice this small.</summary>
        [RelayCommand]
        private void SetTheme(string theme)
        {
            if (!Enum.TryParse<AppThemePreference>(theme, out var preference))
                return;

            SelectedTheme = preference;
            _themeService.SetPreference(preference);
        }

        [RelayCommand]
        private void OpenDataFolder() => _dataManagement.OpenDataFolder();

        /// <summary>First click arms the button; the second, separate click actually deletes.</summary>
        [RelayCommand]
        private async Task DeleteAllDataAsync()
        {
            if (!DeleteArmed)
            {
                DeleteArmed = true;
                DataActionStatus = "Click again to permanently delete all tracked activity.";
                return;
            }

            DeleteArmed = false;
            DataActionStatus = "Deleting...";

            try
            {
                await _dataManagement.DeleteAllActivityAsync();
                DataActionStatus = "All tracked activity has been deleted.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not delete tracked activity");
                DataActionStatus = $"Delete failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CancelDelete()
        {
            DeleteArmed = false;
            DataActionStatus = null;
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
