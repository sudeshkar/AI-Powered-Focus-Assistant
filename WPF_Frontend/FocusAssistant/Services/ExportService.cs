using FocusAssistant.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services
{
    public class ExportService
    {
        private readonly LoggingService _loggingService;
        private readonly DataProcessor _dataProcessor;

        public ExportService(LoggingService loggingService)
        {
            _loggingService = loggingService;
            _dataProcessor = new DataProcessor(loggingService);
        }

        public void ExportMLTrainingData(string filePath, int days = 30)
        {
            try
            {
                var sessions = _loggingService.LoadWorkSessions(days);
                var mlData = _dataProcessor.PrepareMLData(sessions);

                var jsonData = new
                {
                    export_date = DateTime.Now,
                    data_points = mlData.Count,
                    date_range = new
                    {
                        start = sessions.Min(s => s.StartTime),
                        end = sessions.Max(s => s.EndTime)
                    },
                    training_data = mlData
                };

                string json = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);

                Console.WriteLine($"📊 ML Training data exported: {mlData.Count} records to {filePath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export ML training data: {ex.Message}");
            }
        }

        public void ExportDailyReports(string filePath, int days = 30)
        {
            try
            {
                var reports = new List<DailyProductivityReport>();

                for (int i = 0; i < days; i++)
                {
                    var date = DateTime.Today.AddDays(-i);
                    var report = _dataProcessor.GenerateDailyReport(date);
                    reports.Add(report);
                }

                reports = reports.Where(r => r.NumberOfSessions > 0).ToList();

                var exportData = new
                {
                    export_date = DateTime.Now,
                    total_days = reports.Count,
                    date_range = new
                    {
                        start = reports.Min(r => r.Date),
                        end = reports.Max(r => r.Date)
                    },
                    daily_reports = reports
                };

                string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);

                Console.WriteLine($"📈 Daily reports exported: {reports.Count} days to {filePath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export daily reports: {ex.Message}");
            }
        }

        public void ExportToCSV(string filePath, ExportType exportType, int days = 30)
        {
            try
            {
                switch (exportType)
                {
                    case ExportType.RawSessions:
                        ExportSessionsCSV(filePath, days);
                        break;
                    case ExportType.MLTrainingData:
                        ExportMLDataCSV(filePath, days);
                        break;
                    case ExportType.DailyReports:
                        ExportDailyReportsCSV(filePath, days);
                        break;
                    case ExportType.AppUsageDetails:
                        ExportAppUsageCSV(filePath, days);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export CSV: {ex.Message}");
            }
        }

        private void ExportSessionsCSV(string filePath, int days)
        {
            var sessions = _loggingService.LoadWorkSessions(days);

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // Header
                writer.WriteLine("SessionId,Date,StartTime,EndTime,Duration(min),ProductiveTime(min),DistractedTime(min),BreakTime(min),ProductivityScore,AppSwitches,TopApps");

                // Data rows
                foreach (var session in sessions)
                {
                    writer.WriteLine($"{session.SessionId}," +
                                   $"{session.StartTime:yyyy-MM-dd}," +
                                   $"{session.StartTime:HH:mm:ss}," +
                                   $"{session.EndTime:HH:mm:ss}," +
                                   $"{session.Duration.TotalMinutes:F1}," +
                                   $"{session.ProductiveTime.TotalMinutes:F1}," +
                                   $"{session.DistractedTime.TotalMinutes:F1}," +
                                   $"{session.BreakTime.TotalMinutes:F1}," +
                                   $"{session.ProductivityScore:F1}," +
                                   $"{session.AppSwitches}," +
                                   $"\"{string.Join("; ", session.TopApps)}\"");
                }
            }
        }

        private void ExportMLDataCSV(string filePath, int days)
        {
            var sessions = _loggingService.LoadWorkSessions(days);
            var mlData = _dataProcessor.PrepareMLData(sessions);

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // Header
                writer.WriteLine("Timestamp,TimeOfDay,DayOfWeek,CurrentApp,AppCategory,SessionDuration(min),TimeSinceLastSwitch,AppSwitchesLast10Min,AppSwitchesLastHour,ProductivityScoreLast30Min,IsProductive,DistractionLevel");

                // Data rows
                foreach (var record in mlData)
                {
                    writer.WriteLine($"{record.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                                   $"{record.TimeOfDay:F2}," +
                                   $"{record.DayOfWeek}," +
                                   $"\"{record.CurrentApp}\"," +
                                   $"\"{record.AppCategory}\"," +
                                   $"{record.SessionDurationMinutes:F1}," +
                                   $"{record.TimeSinceLastSwitch:F1}," +
                                   $"{record.AppSwitchesLast10Min}," +
                                   $"{record.AppSwitchesLastHour}," +
                                   $"{record.ProductivityScoreLast30Min:F1}," +
                                   $"{record.IsProductive}," +
                                   $"{record.DistractionLevel:F2}");
                }
            }
        }

        private void ExportDailyReportsCSV(string filePath, int days)
        {
            var reports = new List<DailyProductivityReport>();

            for (int i = 0; i < days; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                var report = _dataProcessor.GenerateDailyReport(date);
                if (report.NumberOfSessions > 0)
                    reports.Add(report);
            }

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // Header
                writer.WriteLine("Date,TotalWorkTime(h),ProductiveTime(h),DistractedTime(h),BreakTime(h),ProductivityScore,TotalAppSwitches,AvgSessionLength(min),NumberOfSessions,MostProductiveHour,LeastProductiveHour");

                // Data rows
                foreach (var report in reports.OrderBy(r => r.Date))
                {
                    writer.WriteLine($"{report.Date:yyyy-MM-dd}," +
                                   $"{report.TotalWorkTimeHours:F2}," +
                                   $"{report.ProductiveTimeHours:F2}," +
                                   $"{report.DistractedTimeHours:F2}," +
                                   $"{report.BreakTimeHours:F2}," +
                                   $"{report.ProductivityScore:F1}," +
                                   $"{report.TotalAppSwitches}," +
                                   $"{report.AverageSessionLength:F1}," +
                                   $"{report.NumberOfSessions}," +
                                   $"\"{report.MostProductiveHour}\"," +
                                   $"\"{report.LeastProductiveHour}\"");
                }
            }
        }

        private void ExportAppUsageCSV(string filePath, int days)
        {
            var sessions = _loggingService.LoadWorkSessions(days);
            var allUsages = sessions.SelectMany(s => s.AppUsages).OrderBy(u => u.StartTime);

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // Header
                writer.WriteLine("SessionId,Timestamp,AppName,WindowTitle,Duration(min),IsProductive,DayOfWeek,HourOfDay");

                // Data rows
                foreach (var session in sessions)
                {
                    foreach (var usage in session.AppUsages)
                    {
                        writer.WriteLine($"{session.SessionId}," +
                                       $"{usage.StartTime:yyyy-MM-dd HH:mm:ss}," +
                                       $"\"{usage.AppName}\"," +
                                       $"\"{usage.WindowTitle?.Replace("\"", "''")}\"," +
                                       $"{usage.Duration.TotalMinutes:F1}," +
                                       $"{usage.IsProductive}," +
                                       $"{usage.StartTime.DayOfWeek}," +
                                       $"{usage.StartTime.Hour}");
                    }
                }
            }
        }
    }

    public enum ExportType
    {
        RawSessions,
        MLTrainingData,
        DailyReports,
        AppUsageDetails
    }
}
