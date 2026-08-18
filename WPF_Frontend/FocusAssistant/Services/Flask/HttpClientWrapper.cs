using FocusAssistant.Services.Flask.Interfaces;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Flask
{
    /// <summary>
    /// Thin seam over HttpClient so the endpoint services can be tested without
    /// real sockets.
    /// </summary>
    /// <remarks>
    /// Deliberately not IDisposable: the HttpClient is supplied by
    /// IHttpClientFactory, which owns its lifetime and pools the underlying
    /// handler. Disposing it here tore down a client the factory still managed.
    /// </remarks>
    public class HttpClientWrapper : IHttpClientWrapper
    {
        private readonly HttpClient _httpClient;

        public HttpClientWrapper(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public Task<HttpResponseMessage> GetAsync(string requestUri) =>
            _httpClient.GetAsync(requestUri);

        public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content) =>
            _httpClient.PostAsync(requestUri, content);
    }
}
