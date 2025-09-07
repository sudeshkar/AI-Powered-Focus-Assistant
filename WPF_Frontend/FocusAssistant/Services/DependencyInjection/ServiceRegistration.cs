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
using FocusAssistant.Services.Export_Services;
using FocusAssistant.Services.Export_Services.Interfaces;
using FocusAssistant.Services.Flask;
using FocusAssistant.Services.Flask.Interfaces;
using FocusAssistant.Services.Interfaces;
using FocusAssistant.Services.ML;
using FocusAssistant.Services.Session;
using FocusAssistant.Services.Session.Interfaces;
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
            Console.WriteLine($"Registering FocusAssistant services at {DateTime.Now:HH:mm:ss.fff}");

            // Configuration Services (Singleton - app-wide settings)
            services.AddSingleton<IFocusAssistantConfig, FocusAssistantConfig>();
            services.AddSingleton<IAppCategorizationConfig, AppCategorizationConfig>();
            services.AddSingleton<FlaskConfiguration>();

            // Core Infrastructure Services
            services.AddCoreInfrastructure();

            // Data & Logging Services
            services.AddDataAndLoggingServices();

            // Application Monitoring Services
            services.AddApplicationMonitoring();

            // ML & Analytics Services
            services.AddMachineLearningServices();

            // Export Services
            services.AddExportServices();

            // Flask Integration Services
            services.AddFlaskIntegration();

            // Session Management
            services.AddSessionManagement();

            // Core tracking and analytics services
            services.AddSingleton<WindowTracker>();
            services.AddSingleton<FlaskIntegrationFacade>();

            return services;
        }

        private static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
        {
            // File system abstraction
            services.AddScoped<IFileSystemWrapper, FileSystemWrapper>();

            // HTTP client for Flask communication
            services.AddSingleton<IHttpClientWrapper, HttpClientWrapper>(provider =>
            {
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri("http://127.0.0.1:5000/"),
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return new HttpClientWrapper(httpClient);
            });

            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            return services;
        }

        private static IServiceCollection AddDataAndLoggingServices(this IServiceCollection services)
        {
            // Repository layer (Scoped - per request/operation)
            services.AddScoped<ISessionRepository, FileBasedSessionRepository>();
            services.AddScoped<IActivityRepository, FileBasedActivityRepository>();

            // Logging implementations
            services.AddScoped<IActivityLogger, FileBasedActivityLogger>();
            services.AddScoped<ILoggingService, LoggingService>();

            // Service layer
            services.AddScoped<IActivityManagementService, ActivityManagementService>();

            return services;
        }

        private static IServiceCollection AddApplicationMonitoring(this IServiceCollection services)
        {
            // Window monitoring (Singleton - system-wide monitoring)
            services.AddSingleton<IWindowMonitor, WindowsApiWindowMonitor>();

            // Idle monitoring with configuration
            services.AddSingleton<IIdleMonitor>(provider =>
            {
                var config = provider.GetRequiredService<IFocusAssistantConfig>();
                var idleTimeout = TimeSpan.FromMinutes(config.IdleTimeoutMinutes ?? 2);
                return new WindowsApiIdleMonitor(idleTimeout);
            });

            // Productivity classification
            services.AddScoped<IProductivityClassifier, ProductivityClassifier>();

            return services;
        }

        private static IServiceCollection AddMachineLearningServices(this IServiceCollection services)
        {
            // ML services (Scoped - per analysis operation)
            services.AddScoped<IMLDataProcessor, MLDataProcessor>();

            // Productivity strategies (Scoped - strategy pattern implementation)
            services.AddScoped<RuleBasedProductivityStrategy>();
            services.AddScoped<MLBasedProductivityStrategy>();

            // Strategy factory
            services.AddScoped<IProductivityStrategyFactory>(provider =>
                new ProductivityStrategyFactory(
                    provider.GetRequiredService<RuleBasedProductivityStrategy>(),
                    provider.GetRequiredService<MLBasedProductivityStrategy>()
                ));

            return services;
        }

        private static IServiceCollection AddExportServices(this IServiceCollection services)
        {
            // Export implementations (Scoped - per export operation)
            services.AddScoped<SessionsCsvExporter>();
            services.AddScoped<SessionsJsonExporter>();
            services.AddScoped<DailyReportsCsvExporter>();
            services.AddScoped<DailyReportsJsonExporter>();
            services.AddScoped<AppUsageCsvExporter>();
            services.AddScoped<MLDataCsvExporter>();

            // Export factory and service
            services.AddScoped<IExportFactory, ExportFactory>();
            services.AddScoped<IExportService, ExportService>();

            return services;
        }

        private static IServiceCollection AddFlaskIntegration(this IServiceCollection services)
        {
            // Flask server management
            services.AddSingleton<IPythonExecutableFinder, PythonExecutableFinder>();
            services.AddSingleton<IFlaskServerManager, FlaskServerManager>();

            // Flask API services
            services.AddSingleton<IActivityService, FlaskActivityService>();
            services.AddSingleton<IAnalyticsService, FlaskAnalyticsService>();
            services.AddSingleton<IFeedbackService, FlaskFeedbackService>();
            services.AddSingleton<ISuggestionsService, SuggestionsService>();

            // Flask data service
            services.AddScoped<IFlaskDataService, FlaskDataService>();

            return services;
        }

        private static IServiceCollection AddSessionManagement(this IServiceCollection services)
        {
            // Session management (Scoped - per user session)
            services.AddScoped<ISessionManager, SessionManager>();
            services.AddScoped<IReportGenerator, DailyReportGenerator>();

            return services;
        }
    }
}