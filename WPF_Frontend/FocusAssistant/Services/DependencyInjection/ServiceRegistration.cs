using FocusAssistant.Data;
using FocusAssistant.Models;
using FocusAssistant.Services.Application_Monitoring;
using FocusAssistant.Services.Application_Monitoring.Interfaces;
using FocusAssistant.Services.Config;
using FocusAssistant.Services.Config.interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_layer;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Logging_Implementations;
using FocusAssistant.Services.Data_log_and_Save_Repo.Repository_Implementations;
using FocusAssistant.Services.Data_log_and_Save_Repo.Service_Layer;
using FocusAssistant.Services.Datafetch;
using FocusAssistant.Services.Datafetch.Interfaces;
using FocusAssistant.Services.Export_Services;
using FocusAssistant.Services.Export_Services.Interfaces;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Interfaces;
using FocusAssistant.Services.ML;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
using FocusAssistant.ViewModels;
using FocusAssistant.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace FocusAssistant.Services.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddFocusAssistantServices(this IServiceCollection services)
        {
            // ------------------------
            // Configuration Services
            // ------------------------
            services.AddSingleton<IFocusAssistantConfig, FocusAssistantConfig>();
            services.AddSingleton<IAppCategorizationConfig, AppCategorizationConfig>();
            services.AddSingleton<FlaskConfiguration>();

            // Inside ConfigureServices / AddFocusAssistantServices
            services.AddDbContext<FocusAssistantDbContext>(options =>
            {
                options.UseSqlite("Data Source=FocusAssistant.db"); // or SQL Server if needed
            });


            // ------------------------
            // Core Infrastructure
            // ------------------------
            services.AddScoped<IFileSystemWrapper, FileSystemWrapper>();

            services.AddSingleton<IHttpClientWrapper>(provider =>
            {
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri("http://127.0.0.1:5000/"),
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return new HttpClientWrapper(httpClient);
            });

            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // ------------------------
            // Data & Logging Services
            // ------------------------
            services.AddScoped<ISessionRepository, FileBasedSessionRepository>();
            services.AddScoped<IActivityRepository, FileBasedActivityRepository>();
            services.AddScoped<IActivityLogger, FileBasedActivityLogger>();
            services.AddScoped<ILoggingService, LoggingService>();
            services.AddScoped<IActivityManagementService, ActivityManagementService>();

            // ------------------------
            // Application Monitoring
            // ------------------------
            services.AddSingleton<IWindowMonitor, WindowsApiWindowMonitor>();
            services.AddSingleton<WindowTracker>();

            services.AddScoped<IBaseService<UserSession>, BaseService<UserSession>>();
            services.AddScoped<IBaseService<WorkSession>, BaseService<WorkSession>>();
            services.AddScoped<IBaseService<AppUsage>, BaseService<AppUsage>>();
            services.AddScoped<IBaseService<RLInteraction>, BaseService<RLInteraction>>();

            services.AddScoped<ISessionManager, SessionManager>();

            services.AddScoped<IIdleMonitor>(provider =>
            {
                var config = provider.GetRequiredService<IFocusAssistantConfig>();
                var idleTimeout = TimeSpan.FromMinutes(config.IdleTimeoutMinutes ?? 2);
                return new WindowsApiIdleMonitor(idleTimeout);
            });

            services.AddScoped<IProductivityClassifier, ProductivityClassifier>();
            services.AddScoped<IActivityService, FlaskActivityService>();

            // ------------------------
            // Machine Learning Services
            // ------------------------
            services.AddScoped<IMLDataProcessor, MLDataProcessor>();
            services.AddScoped<RuleBasedProductivityStrategy>();
            services.AddScoped<MLBasedProductivityStrategy>();
            services.AddScoped<IProductivityStrategyFactory>(provider =>
                new ProductivityStrategyFactory(
                    provider.GetRequiredService<RuleBasedProductivityStrategy>(),
                    provider.GetRequiredService<MLBasedProductivityStrategy>()
                ));

            // ------------------------
            // Export Services
            // ------------------------
            services.AddScoped<SessionsCsvExporter>();
            services.AddScoped<SessionsJsonExporter>();
            services.AddScoped<DailyReportsCsvExporter>();
            services.AddScoped<DailyReportsJsonExporter>();
            services.AddScoped<AppUsageCsvExporter>();
            services.AddScoped<MLDataCsvExporter>();
            services.AddScoped<IExportFactory, ExportFactory>();
            services.AddScoped<IExportService, ExportService>();

            // ------------------------
            // Flask Integration
            // ------------------------
            services.AddSingleton<IPythonExecutableFinder, PythonExecutableFinder>();
            services.AddSingleton<IFlaskServerManager, FlaskServerManager>();
            services.AddSingleton<FlaskIntegrationFacade>();
            services.AddSingleton<IAnalyticsService, FlaskAnalyticsService>();
            services.AddSingleton<IFeedbackService, FlaskFeedbackService>();
            services.AddSingleton<ISuggestionsService, SuggestionsService>();
            services.AddScoped<IFlaskDataService, FlaskDataService>();

            // ------------------------
            // Reports & Session Management
            // ------------------------
            services.AddScoped<IReportGenerator, DailyReportGenerator>();

            // ------------------------
            // Views & ViewModels
            // ------------------------
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardView>();
            services.AddTransient<TrackingView>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<TrackingViewModel>();
            services.AddTransient<RecommendationViewModel>();

            return services;
        }
    }
}
