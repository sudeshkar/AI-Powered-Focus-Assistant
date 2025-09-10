using FocusAssistant.Converters;
using FocusAssistant.Data;
using FocusAssistant.Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Datafetch;
using FocusAssistant.Services.Datafetch.Interfaces;
using FocusAssistant.Services.DependencyInjection;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.ML;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.ViewModels;
using FocusAssistant.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using Windows.Services.Maps;

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
                using var db = new FocusAssistantDbContext();
                db.Database.EnsureCreated();  
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
                    // Database Context
                    services.AddDbContext<FocusAssistantDbContext>(options =>
    options.UseSqlite(context.Configuration.GetConnectionString("DefaultConnection")
                      ?? "Data Source=FocusAssistant.db"));


                    // Data Services
                    services.AddScoped<IUserSessionService, UserSessionService>();
                    services.AddScoped<IBaseService<UserSession>, UserSessionService>();
                    services.AddScoped<IAppUsageService, AppUsageService>();
                    services.AddScoped<IWorkSessionService, WorkSessionService>();

                    // Monitoring Services
                    services.AddSingleton<IIdleMonitor, WindowsApiIdleMonitor>();
                    services.AddSingleton<IWindowMonitor, WindowsApiWindowMonitor>();
                    services.AddSingleton<WindowTracker>();
                    services.AddTransient<SessionManager>();

                    // ML Services
                    services.AddScoped<IActivityService, FlaskActivityService>(); // Adjust based on your ML implementation
                    services.AddScoped<IAnalyticsService, AnalyticsService>(); // Adjust based on your ML implementation

                    // Flask Services
                    services.AddSingleton<IFlaskServerManager, FlaskServerManager>();
                    services.AddSingleton<FlaskIntegrationFacade>();


                    services.AddScoped<IAnalyticsService, AnalyticsService>();
                    services.AddScoped<IReportGenerator, DailyReportGenerator>();




                    // Windows and Views
                    services.AddTransient<MainWindow>();
                    services.AddTransient<DashboardView>();
                    services.AddTransient<TrackingView>();
                    services.AddTransient<AnalyticsView>();

                    // ViewModels (ensure they receive dependencies)
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<TrackingViewModel>();
                    services.AddTransient<RecommendationViewModel>();
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
                        await windowTracker.StopTrackingAsync();
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