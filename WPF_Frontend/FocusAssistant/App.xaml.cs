using FocusAssistant.Data;
using FocusAssistant.Services;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Config;
using FocusAssistant.Services.Config.interfaces;
using FocusAssistant.Services.Datafetch;
using FocusAssistant.Services.Datafetch.Interfaces;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.SQL_analytics;
using FocusAssistant.ViewModels;
using FocusAssistant.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace FocusAssistant
{
    /// <summary>
    /// Composition root. Owns the service provider, creates the database, and
    /// brings the Python backend up and down with the application.
    /// </summary>
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
            services.AddSingleton<FlaskConfiguration>();
            services.AddSingleton<IAppCategorizationConfig, AppCategorizationConfig>();

            // ---- Python backend ----
            services.AddSingleton<IPythonExecutableFinder, PythonExecutableFinder>();
            services.AddSingleton<IFlaskServerManager, FlaskServerManager>();

            // Typed client so the handler is pooled and the timeout is set once.
            services.AddHttpClient<IHttpClientWrapper, HttpClientWrapper>((provider, client) =>
            {
                var config = provider.GetRequiredService<FlaskConfiguration>();
                client.Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds);
            });

            services.AddSingleton<IActivityService, FlaskActivityService>();
            services.AddSingleton<IAnalyticsService, FlaskAnalyticsService>();
            services.AddSingleton<IFeedbackService, FlaskFeedbackService>();
            services.AddSingleton<ISuggestionsService, SuggestionsService>();
            services.AddSingleton<FlaskIntegrationFacade>();

            // ---- Monitoring and sessions ----
            services.AddSingleton<IProductivityStrategy, RuleBasedProductivityStrategy>();
            services.AddSingleton<IWindowMonitor>(_ => new WindowsApiWindowMonitor(TimeSpan.FromSeconds(2)));
            services.AddSingleton<IIdleMonitor>(_ => new WindowsApiIdleMonitor(TimeSpan.FromMinutes(2)));
            services.AddSingleton<ISessionManager, SessionManager>();
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

        protected override async void OnStartup(StartupEventArgs e)
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

                // Start the backend after the window is up, so a slow or missing
                // Python install delays AI features rather than the whole app.
                _ = StartBackendAsync();
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

        private async Task StartBackendAsync()
        {
            try
            {
                var server = _serviceProvider!.GetRequiredService<IFlaskServerManager>();
                if (!await server.StartServerAsync())
                    Console.WriteLine("Backend unavailable; AI suggestions will be disabled this session.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Backend startup failed: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Disposing the provider stops the backend process and the monitors,
                // via the IDisposable services it owns. Nothing did this before, so
                // the Python process outlived the app.
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
