using FocusAssistant.Models.Response_Models;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    public interface IFeedbackService
    {
        /// <summary>Reports how the user responded to an intervention.</summary>
        Task SendFeedbackAsync(FeedbackRequest feedback);
    }
}
