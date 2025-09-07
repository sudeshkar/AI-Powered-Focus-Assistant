using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    public interface IActivityService
    {
        Task<ActivityResponse> SendActivityAsync(AppUsage appUsage);
    }
}
