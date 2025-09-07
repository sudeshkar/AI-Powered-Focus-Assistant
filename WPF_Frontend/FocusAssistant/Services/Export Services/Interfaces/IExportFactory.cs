using FocusAssistant.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Export_Services.Interfaces
{
    public interface IExportFactory
    {
        IExporter CreateExporter(ExportType exportType, ExportFormat format);
    }
}
