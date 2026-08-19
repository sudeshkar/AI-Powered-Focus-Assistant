using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Core.Intervention
{
    /// <summary>Shows a suggestion to the user and reports what they did about it.</summary>
    public interface IInterventionDispatcher
    {
        Task<InterventionResponse> ShowAsync(InterventionSuggestion suggestion, CancellationToken ct = default);
    }
}
