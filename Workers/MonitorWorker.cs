using Microsoft.Extensions.Options;
using WebStateNotifier.Models;
using WebStateNotifier.Services;

namespace WebStateNotifier.Workers;

public class MonitorWorker(
    UrlHealthChecker healthChecker,
    EmailService emailService,
    IOptions<MonitorOptions> options,
    ILogger<MonitorWorker> logger) : BackgroundService
{
    private readonly MonitorOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateConfiguration();

        logger.LogInformation(
            "Démarrage du monitoring de {Url} | Vérification UP toutes les {DownDelay}s, DOWN toutes les {UpDelay}s",
            _options.Url, _options.CheckingDownDelaySeconds, _options.CheckingUpDelaySeconds);

        bool isDown = false;
        DateTimeOffset? downSince = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var (isUp, statusCode, error) = await healthChecker.CheckAsync(stoppingToken);

            if (!isUp && !isDown)
            {
                isDown = true;
                downSince = DateTimeOffset.UtcNow;
                logger.LogWarning("Service {Url} est DOWN. Raison: {Error}", _options.Url, error);

                await TrySendEmailAsync(
                    () => emailService.SendDownNotificationAsync(_options.Url, _options.Emails, downSince.Value, stoppingToken),
                    "notification de panne");
            }
            else if (isUp && isDown)
            {
                var downtime = DateTimeOffset.UtcNow - downSince!.Value;
                isDown = false;
                logger.LogInformation("Service {Url} est de nouveau UP après {Downtime}", _options.Url, downtime);

                await TrySendEmailAsync(
                    () => emailService.SendUpNotificationAsync(_options.Url, _options.Emails, DateTimeOffset.UtcNow, downtime, stoppingToken),
                    "notification de rétablissement");

                downSince = null;
            }
            else if (isUp)
            {
                logger.LogDebug("Service {Url} est UP (HTTP {StatusCode})", _options.Url, statusCode);
            }
            else
            {
                logger.LogWarning("Service {Url} est toujours DOWN. Raison: {Error}", _options.Url, error);
            }

            var delaySeconds = isDown
                ? _options.CheckingUpDelaySeconds
                : _options.CheckingDownDelaySeconds;

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task TrySendEmailAsync(Func<Task> sendAction, string label)
    {
        try
        {
            await sendAction();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de l'envoi de la {Label}", label);
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
            throw new InvalidOperationException("Monitor:Url est requis.");

        if (_options.Emails.Count == 0)
            throw new InvalidOperationException("Monitor:Emails doit contenir au moins une adresse.");

        if (_options.CheckingDownDelaySeconds <= 0)
            throw new InvalidOperationException("Monitor:CheckingDownDelaySeconds doit être supérieur à 0.");

        if (_options.CheckingUpDelaySeconds <= 0)
            throw new InvalidOperationException("Monitor:CheckingUpDelaySeconds doit être supérieur à 0.");
    }
}
