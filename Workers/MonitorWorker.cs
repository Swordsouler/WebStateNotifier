using Microsoft.Extensions.Options;
using WebStateNotifier.Models;
using WebStateNotifier.Services;

namespace WebStateNotifier.Workers;

/// <summary>
/// Lance la surveillance de chaque site configuré, en parallèle et indépendamment des autres.
/// </summary>
public class MonitorWorker(
    IConfiguration configuration,
    IOptions<MonitorOptions> options,
    IUrlHealthChecker healthChecker,
    IEmailService emailService,
    TimeProvider timeProvider,
    ILogger<MonitorWorker> logger) : BackgroundService
{
    private readonly MonitorOptions _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var monitors = MonitorConfiguration.Load(configuration);

        logger.LogInformation(
            "Démarrage du monitoring de {Count} site(s) | Vérification UP toutes les {DownDelay}s, DOWN toutes les {UpDelay}s",
            monitors.Count, _settings.CheckingDownDelaySeconds, _settings.CheckingUpDelaySeconds);

        foreach (var monitor in monitors)
        {
            logger.LogInformation(
                "[{Name}] Surveillance de {Url} | {RecipientCount} destinataire(s), alerte après {Failures} échecs de suite",
                monitor.Name, monitor.Url, monitor.Recipients.Count, monitor.FailuresBeforeDown);
        }

        var runners = monitors
            .Select(monitor => new SiteMonitorRunner(monitor, _settings, healthChecker, emailService, timeProvider, logger))
            .ToList();

        await Task.WhenAll(runners.Select(runner => runner.RunAsync(stoppingToken)));
    }
}
