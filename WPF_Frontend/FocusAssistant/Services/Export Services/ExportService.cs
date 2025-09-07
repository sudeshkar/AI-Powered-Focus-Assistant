using FocusAssistant.Enums;
using FocusAssistant.Models;
using FocusAssistant.Services.Export_Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services
{
    public class ExportService : IExportService
    {
        private readonly IExportFactory _exportFactory;

        public ExportService(IExportFactory exportFactory)
        {
            _exportFactory = exportFactory;
        }
        public async Task ExportAsync(ExportRequest request)
        {
            try
            {
                var exporter = _exportFactory.CreateExporter(request.ExportType, request.Format);

                // Use specific methods based on export type
                switch (request.ExportType)
                {
                    case ExportType.Sessions:
                        if (exporter is ISessionsExporter sessionsExporter)
                            await sessionsExporter.ExportSessionsAsync(request.FilePath, request.Days);
                        break;

                    case ExportType.DailyReports:
                        if (exporter is IDailyReportsExporter reportsExporter)
                            await reportsExporter.ExportDailyReportsAsync(request.FilePath, request.Days);
                        break;

                    case ExportType.AppUsage:
                        if (exporter is IAppUsageExporter appUsageExporter)
                            await appUsageExporter.ExportAppUsageAsync(request.FilePath, request.Days);
                        break;

                    case ExportType.MLTrainingData:
                        if (exporter is IMLDataExporter mlDataExporter)
                            await mlDataExporter.ExportMLDataAsync(request.FilePath, request.Days);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export {request.ExportType} as {request.Format}: {ex.Message}", ex);
            }
        }
        // Convenience methods for backward compatibility
        public async Task ExportSessionsCsvAsync(string filePath, int days = 30)
        {
            await ExportAsync(new ExportRequest
            {
                ExportType = ExportType.Sessions,
                Format = ExportFormat.Csv,
                FilePath = filePath,
                Days = days
            });
        }
        public async Task ExportDailyReportsJsonAsync(string filePath, int days = 30)
        {
            await ExportAsync(new ExportRequest
            {
                ExportType = ExportType.DailyReports,
                Format = ExportFormat.Json,
                FilePath = filePath,
                Days = days
            });
        }
    }
}
