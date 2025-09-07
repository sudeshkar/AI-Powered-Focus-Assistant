using FocusAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services.Interfaces
{
    public interface IExportService
    {
        Task ExportAsync(ExportRequest request);
        Task ExportSessionsCsvAsync(string filePath, int days = 30);
        Task ExportDailyReportsJsonAsync(string filePath, int days = 30);
    }
}
