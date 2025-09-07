using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using FocusAssistant.Converters;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.DependencyInjection;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.ML;
using FocusAssistant.Services.Session;
using FocusAssistant.ViewModels;
using FocusAssistant.Views;

namespace FocusAssistant
{
    public partial class App : Application
    {
        private IHost? _host;
        public static IServiceProvider? Services { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                Console.WriteLine($"Starting application at {DateTime.Now:HH:mm:ss.fff}");
                _host = CreateHostBuilder().Build();
                Services = _host.Services;
                Console.WriteLine($"Services initialized at {DateTime.Now:HH:mm:ss.fff}");

                await _host.StartAsync();
                Console.WriteLine($"Host started at {DateTime.Now:HH:mm:ss.fff}");

                var mainWindow = Services.GetRequiredService<MainWindow>();
                var trackingView = Services.GetRequiredService<TrackingView>();
                mainWindow.Content = trackingView;
                Console.WriteLine($"MainWindow created with TrackingView at {DateTime.Now:HH:mm:ss.fff}");
                mainWindow.Show();
                Console.WriteLine($"MainWindow shown at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Application startup failed: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show(
                    $"Application startup failed: {ex.Message}\nDetails: {ex.StackTrace}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private static IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register core services via extension method
                    services.AddFocusAssistantServices();

                    // Windows and Views
                    services.AddTransient<MainWindow>();
                    services.AddTransient<DashboardView>();
                    services.AddTransient<TrackingView>();
                    services.AddTransient<AnalyticsView>();

                    // ViewModels
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<TrackingViewModel>();
                    services.AddTransient<RecommendationViewModel>();

                    // Converters
                    services.AddSingleton<TrackingColorConverter>();
                    services.AddSingleton<TrackingButtonContentConverter>();
                    services.AddSingleton<IdleStatusColorConverter>();
                    services.AddSingleton<ZeroCountToVisibilityConverter>();
                    services.AddSingleton<NullToVisibilityConverter>();
                    services.AddSingleton<BoolToVisibilityConverter>();
                    services.AddSingleton<StringToDoubleConverter>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole(options =>
                    {
                        options.TimestampFormat = "[HH:mm:ss.fff] ";
                        options.IncludeScopes = true;
                    });
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddFilter("Microsoft", LogLevel.Warning);
                    logging.AddFilter("System", LogLevel.Warning);
                });
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_host != null)
                {
                    Console.WriteLine($"Stopping host at {DateTime.Now:HH:mm:ss.fff}");

                    var windowTracker = Services?.GetService<WindowTracker>();
                    if (windowTracker != null)
                    {
                        Console.WriteLine($"Disposing WindowTracker at {DateTime.Now:HH:mm:ss.fff}");
                        await windowTracker.DisposeAsync();
                    }

                    var flaskFacade = Services?.GetService<FlaskIntegrationFacade>();
                    if (flaskFacade != null)
                    {
                        Console.WriteLine($"Stopping Flask server at {DateTime.Now:HH:mm:ss.fff}");
                        flaskFacade.StopServer();
                    }

                    await _host.StopAsync(TimeSpan.FromSeconds(5));
                    _host.Dispose();
                    Console.WriteLine($"Host disposed at {DateTime.Now:HH:mm:ss.fff}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Shutdown error: {ex.Message} at {DateTime.Now:HH:mm:ss.fff}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }
}