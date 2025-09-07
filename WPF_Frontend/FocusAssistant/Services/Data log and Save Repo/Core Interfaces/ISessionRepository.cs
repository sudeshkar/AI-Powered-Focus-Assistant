using FocusAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces
{
    public interface ISessionRepository
    {
        Task<IEnumerable<WorkSession>> GetSessionsAsync(int days = 7);
        Task SaveSessionAsync(WorkSession session);
        Task<IEnumerable<WorkSession>> GetSessionsByDateAsync(DateTime date);
        Task<WorkSession?> GetSessionByIdAsync(string sessionId);
        Task DeleteSessionAsync(string sessionId);
    }
}
