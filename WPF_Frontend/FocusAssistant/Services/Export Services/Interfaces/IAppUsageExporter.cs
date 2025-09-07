using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services.Interfaces
{
    public interface IAppUsageExporter : IExporter
    {
        Task ExportAppUsageAsync(string filePath, int days = 30);
    }
}
