using System.Net.Http;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask.Interfaces
{
    public interface IHttpClientWrapper
    {
        Task<HttpResponseMessage> GetAsync(string requestUri);
        Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content);
    }
}