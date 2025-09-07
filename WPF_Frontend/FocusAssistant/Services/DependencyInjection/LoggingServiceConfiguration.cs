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
using FocusAssistant.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.DependencyInjection
{
    public static class LoggingServiceConfiguration
    {
        public static IServiceCollection AddLoggingServices(this IServiceCollection services)
        {
            services.AddScoped<IFileSystemWrapper, FileSystemWrapper>();
            services.AddScoped<ILoggingService, LoggingService>();
            services.AddScoped<IActivityLogger, FileBasedActivityLogger>();
            services.AddScoped<ISessionRepository, FileBasedSessionRepository>();
            services.AddScoped<IActivityRepository, FileBasedActivityRepository>();
            services.AddScoped<IActivityManagementService, ActivityManagementService>();

            // Exporters (scoped as in your code)
            services.AddScoped<SessionsCsvExporter>();
            services.AddScoped<SessionsJsonExporter>();
            services.AddScoped<DailyReportsCsvExporter>();
            services.AddScoped<DailyReportsJsonExporter>();
            services.AddScoped<AppUsageCsvExporter>();
            services.AddScoped<MLDataCsvExporter>();
            services.AddScoped<IExportFactory, ExportFactory>();
            services.AddScoped<IExportService, ExportService>();

            return services;
        }
    }
}
