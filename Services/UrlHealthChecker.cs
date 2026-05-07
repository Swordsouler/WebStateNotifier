using System.Net.Http;
using Microsoft.Extensions.Options;
using WebStateNotifier.Models;

namespace WebStateNotifier.Services;

public class UrlHealthChecker(IHttpClientFactory httpClientFactory, IOptions<MonitorOptions> options, ILogger<UrlHealthChecker> logger)
{
    private readonly MonitorOptions _options = options.Value;

    public async Task<(bool IsUp, int? StatusCode, string? Error)> CheckAsync(CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient("monitor");
        try
        {
            var response = await client.GetAsync(_options.Url, ct);
            var isUp = response.IsSuccessStatusCode;
            logger.LogDebug("Vérification {Url} → HTTP {StatusCode}", _options.Url, (int)response.StatusCode);
            return (isUp, (int)response.StatusCode, isUp ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Timeout lors de la vérification de {Url}", _options.Url);
            return (false, null, "Timeout");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Erreur réseau pour {Url}: {Message}", _options.Url, ex.Message);
            return (false, null, ex.Message);
        }
    }
}
