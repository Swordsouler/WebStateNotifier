using System.Net.Http;

namespace WebStateNotifier.Services;

public interface IUrlHealthChecker
{
    Task<(bool IsUp, int? StatusCode, string? Error)> CheckAsync(string url, CancellationToken ct);
}

public class UrlHealthChecker(IHttpClientFactory httpClientFactory, ILogger<UrlHealthChecker> logger) : IUrlHealthChecker
{
    public async Task<(bool IsUp, int? StatusCode, string? Error)> CheckAsync(string url, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient("monitor");
        try
        {
            using var response = await client.GetAsync(url, ct);
            var isUp = response.IsSuccessStatusCode;
            logger.LogDebug("Vérification {Url} → HTTP {StatusCode}", url, (int)response.StatusCode);
            return (isUp, (int)response.StatusCode, isUp ? null : $"HTTP {(int)response.StatusCode}");
        }
        // Un arrêt du service n'est pas un échec du site : seule l'expiration du délai HTTP en est un.
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Timeout lors de la vérification de {Url}", url);
            return (false, null, "Timeout");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Erreur réseau pour {Url}: {Message}", url, ex.Message);
            return (false, null, ex.Message);
        }
    }
}
