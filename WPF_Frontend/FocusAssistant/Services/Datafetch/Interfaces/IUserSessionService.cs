using FocusAssistant.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Datafetch.Interfaces
{
    public interface IUserSessionService : IBaseService<UserSession>
    {
        Task<IEnumerable> GetByDateAsync(DateTime date);
    }
}
