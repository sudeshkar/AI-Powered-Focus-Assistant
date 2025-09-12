using FocusAssistant.Models;
using FocusAssistant.Models.Response_Models;
using System;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Session.Interfaces
{
    public interface IReportGenerator
    {
        Task<AnalyticsResponse> GenerateReportInternal(DateTime date);
        Task<AnalyticsResponse> GetReportFlask();

    }
}