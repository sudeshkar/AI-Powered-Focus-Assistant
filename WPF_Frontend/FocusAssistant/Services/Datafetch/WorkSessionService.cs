using FocusAssistant.Data;
using FocusAssistant.Models;
using FocusAssistant.Services.Datafetch.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Datafetch
{
    public class WorkSessionService : BaseService<WorkSession>, IWorkSessionService
    {
        public WorkSessionService(FocusAssistantDbContext context) : base(context)
        {
        }
    }
}
