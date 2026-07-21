using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Core.Sources;

namespace Yukari.Services.Sources;

internal class SharedHttpClient : ISharedHttpClient
{
    private readonly HttpClient _httpClient;

    public SharedHttpClient(HttpClient httpClient) => _httpClient = httpClient;

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct = default
    ) => _httpClient.SendAsync(request, ct);
}
