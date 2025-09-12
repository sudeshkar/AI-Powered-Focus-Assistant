using FocusAssistant.Data;
using FocusAssistant.Models;
using FocusAssistant.Services;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Config;
using FocusAssistant.Services.Config.interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_layer;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Logging_Implementations;
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
using System.Windows;

namespace FocusAssistant
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // ----------------------
            // Database
            // ----------------------
            services.AddDbContext<FocusAssistantDbContext>(options =>
            {
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FocusAssistant",
                    "focusassistant.db"
                );
                Console.WriteLine( dbPath );
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
                options.UseSqlite($"Data Source={dbPath}")
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            }, ServiceLifetime.Scoped);

            // ----------------------
            // Data Services (BaseService implementations)
            // ----------------------
            services.AddScoped<AnalyticsServiceSQL>();
            services.AddSingleton(typeof(IBaseService<>), typeof(BaseService<>));
            services.AddSingleton<IUserSessionService, UserSessionService>();
            services.AddSingleton<IWorkSessionService, WorkSessionService>();
            services.AddSingleton<IAppUsageService, AppUsageService>();
            services.AddSingleton<IRLInteractionService, RLInteractionService>();
            services.AddSingleton<FlaskIntegrationFacade>();
            services.AddSingleton<ISuggestionsService, SuggestionsService>();



            //Analytics View
            services.AddScoped<IBaseService<WorkSession>, WorkSessionService>();
            services.AddScoped<IBaseService<AppUsage>, AppUsageService>();
            services.AddScoped<AnalyticsServiceSQL>();

            // ----------------------
            // Flask / AI Service
            // ----------------------
            services.AddHttpClient();
            services.AddSingleton<IActivityService, FlaskActivityService>();
            services.AddSingleton<IHttpClientWrapper, HttpClientWrapper>(); // if needed by ActivityService
            services.AddSingleton<IFeedbackService, FlaskFeedbackService>();

            services.AddSingleton<RuleBasedProductivityStrategy>();

            // ----------------------
            // Application Monitoring / Productivity
            // ----------------------
            services.AddSingleton<IIdleMonitor, WindowsApiIdleMonitor>();
            services.AddSingleton<IWindowMonitor, WindowsApiWindowMonitor>();
            

            // ----------------------
            // Session Management
            // ----------------------
            services.AddSingleton<ISessionManager, SessionManager>();
            services.AddSingleton<IProductivityStrategy, RuleBasedProductivityStrategy>();
            
            services.AddSingleton<IAppCategorizationConfig, AppCategorizationConfig>();
            services.AddSingleton<IActivityLogger, FileBasedActivityLogger>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IFileSystemWrapper, FileSystemWrapper>();
            services.AddHttpClient<IHttpClientWrapper, HttpClientWrapper>();
            services.AddSingleton<IAnalyticsService, AnalyticsService>();
            services.AddSingleton<FlaskConfiguration>();
            services.AddScoped<IFlaskServerManager, FlaskServerManager>();
            services.AddScoped<IPythonExecutableFinder, PythonExecutableFinder>();
            services.AddScoped<IReportGenerator, DailyReportGenerator>();






            services.AddSingleton<WindowTracker>();
            services.AddTransient<RecommendationsView>(provider =>
                new RecommendationsView(
                    provider.GetRequiredService<ISessionManager>(),
                    provider.GetRequiredService<FlaskIntegrationFacade>()
                ));

            // ----------------------
            // Views / ViewModels
            // ----------------------
            services.AddTransient<MainWindow>();
            services.AddTransient<TrackingView>();
            services.AddTransient<TrackingViewModel>();
            services.AddTransient<DashboardView>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<AnalyticsView>();
           



            // ----------------------
            // Other Views / placeholders
            // ----------------------
            // You can add more views here as you implement them
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Resolve and show MainWindow
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        protected override async void OnExit(ExitEventArgs e)
        {
             
            base.OnExit(e);
        }
    }
}
