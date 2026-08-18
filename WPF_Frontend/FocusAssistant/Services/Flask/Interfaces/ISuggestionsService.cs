using FocusAssistant.Models.Response_Models;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    public interface ISuggestionsService
    {
        /// <summary>Behavioural patterns, or null when the backend is unreachable.</summary>
        Task<SuggestionsResponse?> GetSuggestionsAsync();
    }
}
