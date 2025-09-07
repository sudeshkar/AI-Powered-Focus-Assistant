using FocusAssistant.Models;
using System;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Session.Interfaces
{
    public interface IReportGenerator
    {
        Task<DailyReport> GenerateReportAsync(DateTime date);
    }
}