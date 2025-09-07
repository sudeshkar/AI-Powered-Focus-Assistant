using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    public interface IFlaskServerManager
    {
        Task<bool> StartServerAsync();
        Task<bool> IsServerHealthyAsync();
        void StopServer();
    }
}
