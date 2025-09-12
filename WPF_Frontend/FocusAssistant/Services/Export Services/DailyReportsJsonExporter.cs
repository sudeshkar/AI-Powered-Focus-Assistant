using FocusAssistant.Models;
using FocusAssistant.Services.Export_Services.Interfaces;
using FocusAssistant.Services.Session.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services
{
    public class DailyReportsJsonExporter //: IDailyReportsExporter, IJsonExporter
    {
        private readonly IReportGenerator _reportGenerator;

        public DailyReportsJsonExporter(IReportGenerator reportGenerator)
        {
            _reportGenerator = reportGenerator;
        }

        //public async Task ExportAsync(string filePath)
        //{
        //    await ExportDailyReportsAsync(filePath);
        //}

        //public async Task ExportDailyReportsAsync(string filePath, int days = 30)
        //{
        //    var reports = new List<DailyProductivityReport>();

        //    // Loop through the last `days` days
        //    for (int i = 0; i < days; i++)
        //    {
        //        var date = DateTime.Today.AddDays(-i);

        //        // Generate the report using the report generator
        //        var dailyReport = await _reportGenerator.GenerateReportInternal(date);

        //        // Only add if there is actual data
        //        if (dailyReport.NumberOfSessions > 0)
        //            reports.Add(dailyReport);
        //    }

        //    // Build JSON structure
        //    var exportData = new
        //    {
        //        export_date = DateTime.Now,
        //        total_days = reports.Count,
        //        date_range = new
        //        {
        //            start = reports.Any() ? reports.Min(r => r.Date) : DateTime.Today,
        //            end = reports.Any() ? reports.Max(r => r.Date) : DateTime.Today
        //        },
        //        daily_reports = reports
        //    };

        //    // Convert to JSON
        //    string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);

        //    // Save to file
        //    await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

        //    Console.WriteLine($"📈 Daily reports JSON exported: {reports.Count} days to {filePath}");
        //}
    }
}
