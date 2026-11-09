using WebStateNotifier.Models;
using WebStateNotifier.Services;

namespace WebStateNotifier.Workers;

/// <summary>
/// Surveille un seul site avec son propre état : compteur d'échecs, panne en cours, rétablissement.
/// Ses alertes ne partent qu'à son groupe de destinataires.
/// </summary>
public sealed class SiteMonitorRunner(
    SiteMonitor monitor,
    MonitorOptions settings,
    IUrlHealthChecker healthChecker,
    IEmailService emailService,
    TimeProvider timeProvider,
    ILogger logger)
{
    private DateTimeOffset? _firstFailureAt;
    private DateTimeOffset? _downSince;

    public SiteMonitor Monitor => monitor;
    public bool IsDown { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    // Un échec est revérifié au rythme de la panne, pour confirmer vite sans attendre le prochain contrôle normal.
    public TimeSpan NextDelay => TimeSpan.FromSeconds(IsDown || ConsecutiveFailures > 0
        ? settings.CheckingUpDelaySeconds
        : settings.CheckingDownDelaySeconds);

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Une erreur imprévue ne doit arrêter ni la surveillance de ce site ni celle des autres.
                logger.LogError(ex, "[{Name}] Erreur inattendue pendant la vérification de {Url}", monitor.Name, monitor.Url);
            }

            await Task.Delay(NextDelay, stoppingToken);
        }
    }

    public async Task CheckOnceAsync(CancellationToken ct)
    {
        var (isUp, statusCode, error) = await healthChecker.CheckAsync(monitor.Url, ct);
        var now = timeProvider.GetUtcNow();

        if (!isUp && !IsDown)
        {
            ConsecutiveFailures++;
            _firstFailureAt ??= now;

            if (ConsecutiveFailures < monitor.FailuresBeforeDown)
            {
                logger.LogWarning("[{Name}] Service {Url} en échec ({Failures}/{Threshold}). Raison: {Error}",
                    monitor.Name, monitor.Url, ConsecutiveFailures, monitor.FailuresBeforeDown, error);
                return;
            }

            IsDown = true;
            _downSince = _firstFailureAt;
            logger.LogWarning("[{Name}] Service {Url} est DOWN. Raison: {Error}", monitor.Name, monitor.Url, error);

            await TrySendEmailAsync(
                () => emailService.SendDownNotificationAsync(monitor, _downSince.Value, ct),
                "notification de panne");
        }
        else if (isUp && IsDown)
        {
            var downtime = now - _downSince!.Value;
            IsDown = false;
            ConsecutiveFailures = 0;
            _firstFailureAt = null;
            _downSince = null;
            logger.LogInformation("[{Name}] Service {Url} est de nouveau UP après {Downtime}", monitor.Name, monitor.Url, downtime);

            await TrySendEmailAsync(
                () => emailService.SendUpNotificationAsync(monitor, now, downtime, ct),
                "notification de rétablissement");
        }
        else if (isUp)
        {
            if (ConsecutiveFailures > 0)
            {
                logger.LogInformation("[{Name}] Service {Url} de nouveau joignable après {Failures} échec(s), aucune alerte envoyée",
                    monitor.Name, monitor.Url, ConsecutiveFailures);
                ConsecutiveFailures = 0;
                _firstFailureAt = null;
            }

            logger.LogDebug("[{Name}] Service {Url} est UP (HTTP {StatusCode})", monitor.Name, monitor.Url, statusCode);
        }
        else
        {
            logger.LogWarning("[{Name}] Service {Url} est toujours DOWN. Raison: {Error}", monitor.Name, monitor.Url, error);
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
            logger.LogError(ex, "[{Name}] Échec de l'envoi de la {Label}", monitor.Name, label);
        }
    }
}
