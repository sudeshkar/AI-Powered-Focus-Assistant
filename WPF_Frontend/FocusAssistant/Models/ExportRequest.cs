using FocusAssistant.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Models
{
    public class ExportRequest
    {
        public ExportType ExportType { get; set; }
        public ExportFormat Format { get; set; }
        public string FilePath { get; set; }
        public int Days { get; set; } = 30;
    }
}
