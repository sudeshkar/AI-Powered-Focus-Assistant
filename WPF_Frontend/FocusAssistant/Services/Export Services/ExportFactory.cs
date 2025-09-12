using FocusAssistant.Enums;
using FocusAssistant.Services.Export_Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FocusAssistant.Services.Export_Services
{
    public class ExportFactory : IExportFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ExportFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IExporter CreateExporter(ExportType exportType, ExportFormat format)
        {
            return (exportType, format) switch
            {
                (ExportType.Sessions, ExportFormat.Csv) => _serviceProvider.GetRequiredService<SessionsCsvExporter>(),
                (ExportType.Sessions, ExportFormat.Json) => _serviceProvider.GetRequiredService<SessionsJsonExporter>(),

                (ExportType.DailyReports, ExportFormat.Csv) => _serviceProvider.GetRequiredService<DailyReportsCsvExporter>(),
               // (ExportType.DailyReports, ExportFormat.Json) => _serviceProvider.GetRequiredService<DailyReportsJsonExporter>(),

                (ExportType.AppUsage, ExportFormat.Csv) => _serviceProvider.GetRequiredService<AppUsageCsvExporter>(),

                (ExportType.MLTrainingData, ExportFormat.Csv) => _serviceProvider.GetRequiredService<MLDataCsvExporter>(),

                _ => throw new NotSupportedException($"Export combination {exportType} + {format} is not supported")
            };
        }

        
    }
}
