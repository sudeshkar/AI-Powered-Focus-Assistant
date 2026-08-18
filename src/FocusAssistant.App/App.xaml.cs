using FocusAssistant.Core.Config;
using FocusAssistant.Core.Data.Abstractions;
using FocusAssistant.Core.Focus;
using FocusAssistant.Core.Monitoring;
using FocusAssistant.Core.Reports;
using FocusAssistant.Core.Session;
using FocusAssistant.Data.EF;
using FocusAssistant.Data.Queries;
using FocusAssistant.Platform.Monitoring;
using FocusAssistant.ViewModels;
using FocusAssistant.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;

namespace FocusAssistant
{
    /// <summary>
    /// Composition root. Owns the service provider and creates the database.
    /// </summary>
    /// <remarks>
    /// This used to also launch, health-check, and tear down a Python Flask
    /// process, so the app could not run without a Python install and a matching
    /// virtual environment. The whole subprocess-management problem is gone: the
    /// focus engine and the intervention pipeline it feeds (Phase 4) run in this
    /// process.
    /// </remarks>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        private static string DatabasePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FocusAssistant",
            "focusassistant.db");

        private void ConfigureServices(IServiceCollection services)
        {
            // ---- Database ----
            // A factory rather than a scoped DbContext: the consumers below are
            // singletons driven by polling timers, and a shared context would be
            // used concurrently from several threads.
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            services.AddDbContextFactory<FocusAssistantDbContext>(options =>
                options.UseSqlite($"Data Source={DatabasePath}"));

            services.AddSingleton(typeof(IBaseService<>), typeof(BaseService<>));
            services.AddSingleton<AnalyticsServiceSQL>();

            // ---- Configuration ----
            services.AddSingleton<IAppCategorizationConfig, AppCategorizationConfig>();

            // ---- Monitoring and sessions ----
            services.AddSingleton<IProductivityStrategy, RuleBasedProductivityStrategy>();
            services.AddSingleton<IWindowMonitor>(_ => new WindowsApiWindowMonitor(TimeSpan.FromSeconds(2)));
            services.AddSingleton<IIdleMonitor>(_ => new WindowsApiIdleMonitor(TimeSpan.FromMinutes(2)));
            services.AddSingleton<ISessionEngine, SessionEngine>();
            services.AddSingleton<WindowTracker>();
            services.AddSingleton<IReportGenerator, DailyReportGenerator>();

            // ---- Views and view models ----
            services.AddSingleton<MainWindow>();
            services.AddSingleton<TrackingViewModel>();
            services.AddSingleton<TrackingView>();
            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<AnalyticsView>();
            services.AddSingleton<RecommendationViewModel>();
            services.AddSingleton<RecommendationsView>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<DashboardView>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var services = new ServiceCollection();
                ConfigureServices(services);

                // Validate on build so lifetime mistakes surface here rather than as
                // a confusing failure deep in a view.
                _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true,
                });

                InitializeDatabase();

                MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                MainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Focus Assistant could not start.\n\n{ex.Message}",
                    "Startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void InitializeDatabase()
        {
            var factory = _serviceProvider!.GetRequiredService<IDbContextFactory<FocusAssistantDbContext>>();
            using var context = factory.CreateDbContext();
            context.Database.EnsureCreated();
            Console.WriteLine($"Database ready at {DatabasePath}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _serviceProvider?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during shutdown: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}
