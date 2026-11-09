using MimeKit;
using WebStateNotifier.Models;

namespace WebStateNotifier.Services;

/// <summary>
/// Lit et valide les sites à surveiller : chaque entrée de « Monitors », plus l'ancien format
/// « Monitor » (Url + Emails) quand il est renseigné, qui reste surveillé à côté des nouveaux.
/// </summary>
public static class MonitorConfiguration
{
    // Une adresse sans domaine (ou avec un « ＠ » qui n'est pas le vrai arobase) serait refusée
    // par le serveur SMTP au moment de la panne : on la refuse dès le démarrage.
    private static readonly ParserOptions StrictAddressParsing = new() { AllowAddressesWithoutDomain = false };

    public static IReadOnlyList<SiteMonitor> Load(IConfiguration configuration)
    {
        var settings = configuration.GetSection(MonitorOptions.SectionName).Get<MonitorOptions>() ?? new MonitorOptions();
        ValidateSettings(settings);

        var monitors = new List<SiteMonitor>();

        if (!string.IsNullOrWhiteSpace(settings.Url))
        {
            var legacy = new SiteMonitorOptions { Name = settings.Name, Url = settings.Url, Recipients = settings.Emails };
            monitors.Add(Resolve(MonitorOptions.SectionName, legacy, nameof(MonitorOptions.Emails), settings));
        }

        foreach (var section in configuration.GetSection(SiteMonitorOptions.SectionName).GetChildren())
        {
            var site = section.Get<SiteMonitorOptions>() ?? new SiteMonitorOptions();
            monitors.Add(Resolve(ToVariableName(section.Path), site, nameof(SiteMonitorOptions.Recipients), settings));
        }

        if (monitors.Count == 0)
            throw new InvalidOperationException(
                "Aucun site à surveiller : renseignez Monitors__0__Url et Monitors__0__Recipients__0 (ou l'ancien Monitor__Url).");

        return monitors;
    }

    private static void ValidateSettings(MonitorOptions settings)
    {
        if (settings.CheckingDownDelaySeconds <= 0)
            throw new InvalidOperationException("Monitor__CheckingDownDelaySeconds doit être supérieur à 0.");

        if (settings.CheckingUpDelaySeconds <= 0)
            throw new InvalidOperationException("Monitor__CheckingUpDelaySeconds doit être supérieur à 0.");

        if (settings.HttpTimeoutSeconds <= 0)
            throw new InvalidOperationException("Monitor__HttpTimeoutSeconds doit être supérieur à 0.");
    }

    private static SiteMonitor Resolve(string prefix, SiteMonitorOptions site, string recipientsKey, MonitorOptions settings)
    {
        var url = site.Url.Trim();
        if (url.Length == 0)
            throw new InvalidOperationException($"{prefix}__Url est requis.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException($"{prefix}__Url doit être une adresse http(s) complète : « {url} ».");

        var recipients = site.Recipients
            .Select(address => address.Trim())
            .Where(address => address.Length > 0)
            .ToList();

        if (recipients.Count == 0)
            throw new InvalidOperationException($"{prefix}__{recipientsKey} doit contenir au moins une adresse.");

        foreach (var address in recipients)
        {
            if (!MailboxAddress.TryParse(StrictAddressParsing, address, out _))
                throw new InvalidOperationException($"{prefix}__{recipientsKey} contient une adresse invalide : « {address} ».");
        }

        var failuresBeforeDown = site.FailuresBeforeDown ?? settings.FailuresBeforeDown;
        if (failuresBeforeDown <= 0)
        {
            var key = site.FailuresBeforeDown is null ? "Monitor__FailuresBeforeDown" : $"{prefix}__FailuresBeforeDown";
            throw new InvalidOperationException($"{key} doit être supérieur à 0.");
        }

        var name = string.IsNullOrWhiteSpace(site.Name) ? uri.Host : site.Name.Trim();

        return new SiteMonitor(name, url, recipients, failuresBeforeDown);
    }

    // Les messages d'erreur citent les variables telles qu'on les écrit dans le .env.
    private static string ToVariableName(string configurationPath) =>
        configurationPath.Replace(ConfigurationPath.KeyDelimiter, "__");
}
