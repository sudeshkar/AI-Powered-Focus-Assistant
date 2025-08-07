using FocusAssistant.Models;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace FocusAssistant.Services
{
    public class WindowTracker
    {
        // Windows API imports
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        private IdleTimeDetector _idleDetector;
        private SessionManager _sessionManager;
        private readonly FlaskIntegrationService _flask = new();
        public WindowTracker(LoggingService loggingService, IdleTimeDetector idleDetector, SessionManager sessionManager)
        {
            _loggingService = loggingService;
            _idleDetector = idleDetector;
            _sessionManager = sessionManager;
            _sessionLogs = new List<AppUsage>();
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private Timer _trackingTimer;
        private bool _isTracking = false;
        private AppUsage _currentAppUsage;
        private List<AppUsage> _sessionLogs;
        private readonly LoggingService _loggingService;

        public event EventHandler<AppUsage> AppSwitched;
        public event EventHandler<List<AppUsage>> SessionCompleted;

        public bool IsTracking => _isTracking;
        public List<AppUsage> CurrentSession => _sessionLogs;

        public WindowTracker(LoggingService loggingService)
        {
            _loggingService = loggingService;
            _sessionLogs = new List<AppUsage>();
        }

        private async void NotifyAiAsync(AppUsage usage)
        {
            try
            {
                var resp = await _flask.SendActivityAsync(usage);

                if (!string.IsNullOrEmpty(resp.InterventionMessage))
                {
                    new ToastContentBuilder()
                    .AddText("Focus Assistant")
                    .AddText(resp.InterventionMessage)
                    .Show();

                    Console.Write("Was this helpful? [y/n] ");
                    var key = Console.ReadKey().Key;
                    await _flask.SendFeedbackAsync(
                            helpful: key == ConsoleKey.Y,
                            action: key == ConsoleKey.Y ? "acted" : "ignored",
                            interventionId: resp.InterventionId);
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AI call failed: {ex.Message}");
            }
        }

        public void StartTracking()
        {
            if (_isTracking) return;

            _isTracking = true;
            _sessionLogs.Clear();

            // Start idle detection
            _idleDetector.StartMonitoring();

            // Start work session
            _sessionManager.StartSession();

            // Track every 2 seconds
            _trackingTimer = new Timer(TrackCurrentWindow, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            Console.WriteLine("🔍 Window tracking started with idle detection...");
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            _isTracking = false;
            _trackingTimer?.Dispose();

            // Stop idle detection
            _idleDetector.StopMonitoring();

            // Finalize current app usage
            if (_currentAppUsage != null)
            {
                _currentAppUsage.EndTime = DateTime.Now;
                _currentAppUsage.Duration = _currentAppUsage.EndTime - _currentAppUsage.StartTime;
                _sessionLogs.Add(_currentAppUsage);
                _sessionManager.AddAppUsage(_currentAppUsage);
            }

            // End work session
            _sessionManager.EndSession();

            // Save session data
            _loggingService.SaveSession(_sessionLogs);
            SessionCompleted?.Invoke(this, _sessionLogs);

            Console.WriteLine($"⏹️ Tracking stopped. Logged {_sessionLogs.Count} app switches.");
        }

        private void TrackCurrentWindow(object state)
        {
            try
            {
                // Skip tracking if user is idle
                if (_idleDetector.IsIdle)
                    return;

                var (appName, windowTitle) = GetActiveWindowInfo();

                if (string.IsNullOrEmpty(appName)) return;

                // Check if we switched to a different app
                if (_currentAppUsage == null ||
                    _currentAppUsage.AppName != appName ||
                    _currentAppUsage.WindowTitle != windowTitle)
                {
                    // Finalize previous app usage
                    if (_currentAppUsage != null)
                    {
                        _currentAppUsage.EndTime = DateTime.Now;
                        _currentAppUsage.Duration = _currentAppUsage.EndTime - _currentAppUsage.StartTime;

                        // Only log if used for more than 3 seconds
                        if (_currentAppUsage.Duration.TotalSeconds >= 3)
                        {
                            _sessionLogs.Add(_currentAppUsage);
                            _sessionManager.AddAppUsage(_currentAppUsage);
                            AppSwitched?.Invoke(this, _currentAppUsage);
                            if (_currentAppUsage.Duration.TotalSeconds >= 10)
                                NotifyAiAsync(_currentAppUsage);
                        }
                    }

                    // Start tracking new app
                    _currentAppUsage = new AppUsage
                    {
                        AppName = appName,
                        WindowTitle = windowTitle,
                        StartTime = DateTime.Now,
                        IsProductive = DetermineProductivity(appName, windowTitle)
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error tracking window: {ex.Message}");
            }
        }

        private (string appName, string windowTitle) GetActiveWindowInfo()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();

                if (handle == IntPtr.Zero)
                    return (null, null);

                // Get window title
                int length = GetWindowTextLength(handle);
                StringBuilder windowTitle = new StringBuilder(length + 1);
                GetWindowText(handle, windowTitle, windowTitle.Capacity);

                // Get process name
                GetWindowThreadProcessId(handle, out uint processId);
                Process process = Process.GetProcessById((int)processId);
                string appName = process.ProcessName;

                return (appName, windowTitle.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting window info: {ex.Message}");
                return (null, null);
            }
        }

        private bool DetermineProductivity(string appName, string windowTitle)
        {
            // Simple productivity classification (you can enhance this)
            var productiveApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "devenv", "code", "notepad", "notepad++", "sublime_text",
                "atom", "pycharm", "intellij", "eclipse", "netbeans",
                "word", "excel", "powerpoint", "outlook", "teams",
                "slack", "zoom", "discord", "figma", "photoshop"
            };

            var distractingApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "chrome", "firefox", "edge", "safari", "opera",
                "spotify", "vlc", "netflix", "youtube", "tiktok",
                "instagram", "facebook", "twitter", "reddit",
                "steam", "epicgameslauncher", "uplay"
            };

            if (productiveApps.Contains(appName))
                return true;

            if (distractingApps.Contains(appName))
            {
                // Check if it's work-related based on window title
                return IsWorkRelatedContent(windowTitle);
            }

            // Default: consider unknown apps as neutral/productive
            return true;
        }

        private bool IsWorkRelatedContent(string windowTitle)
        {
            var workKeywords = new[] { "github", "stackoverflow", "documentation", "tutorial", "course", "learning" };
            var lowerTitle = windowTitle.ToLower();

            return Array.Exists(workKeywords, keyword => lowerTitle.Contains(keyword));
        }

        public void Dispose()
        {
            StopTracking();
            _trackingTimer?.Dispose();
        }
    }
}
