using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Export_Services.Interfaces;
using FocusAssistant.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services
{
    public class MLDataCsvExporter : IMLDataExporter, ICsvExporter
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IMLDataProcessor _mlDataProcessor;

        public MLDataCsvExporter(ISessionRepository sessionRepository, IMLDataProcessor mlDataProcessor)
        {
            _sessionRepository = sessionRepository;
            _mlDataProcessor = mlDataProcessor;
        }

        public async Task ExportAsync(string filePath)
        {
            await ExportMLDataAsync(filePath);
        }

        public async Task ExportMLDataAsync(string filePath, int days = 30)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Fetch sessions and prepare ML data asynchronously
                var sessions = await _sessionRepository.GetSessionsAsync(days);
                var mlData = await _mlDataProcessor.PrepareMLDataAsync(sessions); // Await the Task

                // Offload file writing to a background thread
                await Task.Run(async () =>
                {
                    using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                    // Header
                    await writer.WriteLineAsync("Timestamp,TimeOfDay,DayOfWeek,CurrentApp,AppCategory,SessionDuration(min),TimeSinceLastSwitch,AppSwitchesLast10Min,AppSwitchesLastHour,ProductivityScoreLast30Min,IsProductive,DistractionLevel");

                    // Data rows
                    foreach (var record in mlData)
                    {
                        await writer.WriteLineAsync($"{record.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                                                    $"{record.TimeOfDay:F2}," +
                                                    $"{record.DayOfWeek}," +
                                                    $"\"{record.CurrentApp.Replace("\"", "\"\"")}\"," + // Escape quotes
                                                    $"\"{record.AppCategory.Replace("\"", "\"\"")}\"," + // Escape quotes
                                                    $"{record.SessionDurationMinutes:F1}," +
                                                    $"{record.TimeSinceLastSwitch:F1}," +
                                                    $"{record.AppSwitchesLast10Min}," +
                                                    $"{record.AppSwitchesLastHour}," +
                                                    $"{record.ProductivityScoreLast30Min:F1}," +
                                                    $"{record.IsProductive}," +
                                                    $"{record.DistractionLevel:F2}");
                    }
                });

                Console.WriteLine($"🤖 ML data CSV exported: {mlData.Count} records to {filePath} in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export ML data CSV: {ex.Message}");
                throw; // Rethrow to allow caller to handle (e.g., show error in UI)
            }
        }
    }
}