using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FocusAssistant.Models.Response_Models
{
    // POST /activity request body
    public class ActivityRequest
    {
        [JsonPropertyName("app_name")]
        public string AppName { get; set; }

        [JsonPropertyName("window_title")]
        public string WindowTitle { get; set; }

        [JsonPropertyName("is_productive")]
        public bool IsProductive { get; set; }
    }
}
