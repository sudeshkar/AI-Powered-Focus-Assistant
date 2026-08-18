using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    /// <summary>Owns the lifetime of the Python backend process.</summary>
    public interface IFlaskServerManager
    {
        /// <summary>Starts the backend if needed and waits until it answers /health.</summary>
        Task<bool> StartServerAsync();

        /// <summary>True when the backend responds to /health.</summary>
        Task<bool> IsServerHealthyAsync();

        void StopServer();
    }
}
