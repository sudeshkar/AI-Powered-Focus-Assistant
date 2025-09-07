using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FocusAssistant.Models.Response_Models
{
    public class FeedbackResponse : BaseResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
