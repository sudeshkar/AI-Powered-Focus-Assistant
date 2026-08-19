using FocusAssistant.Core.Session;
using FocusAssistant.Hosting;
using FocusAssistant.Platform.Interop;
using FocusAssistant.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FocusAssistant
{
    /// <summary>
    /// WPF entry point. Owns the host and gets a window on screen as fast as possible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to launch, health-check, and tear down a Python Flask process, so the
    /// app could not run without a Python install and a matching virtual environment.
    /// That is gone: the focus engine and everything it feeds run in this process.
    /// </para>
    /// <para>
    /// <b>Startup is deliberately non-blocking.</b> <see cref="OnStartup"/> has to be
    /// synchronous — WPF gives no choice — so it does only work that cannot fail slowly:
    /// build the object graph, show the window, and hand off. Migrations, model loading,
    /// and tracking are hosted services started afterwards on a background thread, and
    /// the shell binds to <see cref="StartupState"/> to show what is still warming up.
    /// The previous version ran EnsureCreated() before Show(), so a slow disk was a
    /// blank screen with no explanation.
    /// </para>
    /// </remarks>
    public partial class App : Application
    {
        private IHost? _host;
        private SingleInstanceGuard? _instanceGuard;
        private TrayIconHost? _tray;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            RegisterGlobalExceptionHandlers();

            // Two copies would mean two window monitors, two overlapping sessions, and two
            // writers on one SQLite file. Easy to reach now that closing the window only
            // hides it - the natural thing to do next is click the shortcut again.
            _instanceGuard = SingleInstanceGuard.TryAcquire();
            if (_instanceGuard is null)
            {
                Shutdown(0);
                return;
            }

            try
            {
                _host = AppHost.Build();

                // The window closes to the tray rather than exiting, so the app must not
                // shut down when it disappears.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                MainWindow = _host.Services.GetRequiredService<MainWindow>();
                MainWindow.Show();

                _instanceGuard.OnSecondInstanceLaunched(
                    () => Dispatcher.Invoke(ShowMainWindow));

                _tray = new TrayIconHost(
                    _host.Services.GetRequiredService<ISessionEngine>(),
                    showWindow: ShowMainWindow,
                    exit: ExitApplication);
                _tray.Initialize();

                // Not awaited on purpose: OnStartup must return so the window can paint.
                // StartHostAsync owns every failure from here on.
                _ = StartHostAsync();
            }
            catch (Exception ex)
            {
                // Only graph construction can land here, and a graph that will not build
                // is not something the app can run without.
                MessageBox.Show(
                    $"Focus Assistant could not start.\n\n{ex.Message}",
                    "Startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private async Task StartHostAsync()
        {
            try
            {
                await _host!.StartAsync();
            }
            catch (Exception ex)
            {
                // A hosted service that failed leaves the app degraded, not dead — the
                // window is already up and the user should be told which part is missing.
                _host!.Services.GetRequiredService<ILogger<App>>()
                    .LogError(ex, "Host failed to start");
                _host.Services.GetRequiredService<StartupState>().FailureMessage =
                    $"Some background services could not start: {ex.Message}";
            }
        }

        /// <summary>
        /// Catches what the per-timer try/catch blocks cannot: anything thrown on the
        /// dispatcher or an unobserved task. Without these, a throw on a threadpool
        /// thread ends the process with no dialog and nothing in the log.
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += (_, args) =>
            {
                TryLog(args.Exception, "Unhandled dispatcher exception");
                MessageBox.Show(
                    $"Something went wrong.\n\n{args.Exception.Message}",
                    "Focus Assistant", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Handled: a failure while rendering one view is not a reason to lose the
                // tracking session running behind it.
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                TryLog(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                TryLog(args.ExceptionObject as Exception, "Fatal unhandled exception");
        }

        private void TryLog(Exception? ex, string message)
        {
            try
            {
                _host?.Services.GetService<ILogger<App>>()?.LogError(ex, "{Message}", message);
            }
            catch
            {
                // Logging must never be the thing that takes the process down.
            }
        }

        /// <summary>Restores the window from the tray, or from a second launch.</summary>
        private void ShowMainWindow()
        {
            if (MainWindow is null)
                return;

            MainWindow.Show();
            if (MainWindow.WindowState == WindowState.Minimized)
                MainWindow.WindowState = WindowState.Normal;

            MainWindow.Activate();
            _tray?.UpdateTooltip();
        }

        /// <summary>
        /// The only real way out. Marks the window so its Closing handler stops cancelling,
        /// then shuts down, which drains the write queue and closes the session properly.
        /// </summary>
        private void ExitApplication()
        {
            if (MainWindow is MainWindow window)
                window.IsClosingForReal = true;

            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_host is not null)
                {
                    // Bounded: a hosted service that hangs on shutdown must not leave a
                    // zombie process holding the SQLite file.
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    _host.StopAsync(timeout.Token).GetAwaiter().GetResult();
                    _host.Dispose();
                }

                _tray?.Dispose();
                _instanceGuard?.Dispose();
            }
            catch (Exception ex)
            {
                TryLog(ex, "Error during shutdown");
            }

            base.OnExit(e);
        }
    }
}
