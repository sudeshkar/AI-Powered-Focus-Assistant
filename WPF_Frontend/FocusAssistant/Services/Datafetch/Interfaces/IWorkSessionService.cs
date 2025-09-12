using FocusAssistant.Models;
using System.Collections;

namespace FocusAssistant.Services.Datafetch.Interfaces
{
    public interface IWorkSessionService : IBaseService<WorkSession>
    {
        Task<IEnumerable<WorkSession>> GetByDateAsync(DateTime date);
    }
}