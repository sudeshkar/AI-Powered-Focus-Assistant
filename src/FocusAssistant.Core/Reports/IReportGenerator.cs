using System;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Reports
{
    public interface IReportGenerator
    {
        /// <summary>Builds a report for the given date from locally stored sessions.</summary>
        Task<DailyFocusReport> GenerateReportAsync(DateTime date);

        /// <summary>Convenience for the common case of "today, right now".</summary>
        Task<DailyFocusReport> GetTodayReportAsync();
    }
}
