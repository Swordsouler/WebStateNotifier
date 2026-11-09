namespace WebStateNotifier.Models;

/// <summary>
/// Section « Monitor » : réglages communs à tous les sites surveillés.
/// Elle porte aussi l'ancien format à un seul site (Url, Name, Emails), toujours lu comme un moniteur
/// à part entière pour que les configurations existantes marchent telles quelles.
/// </summary>
public class MonitorOptions
{
    public const string SectionName = "Monitor";

    // Ancien format : un seul site.
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Emails { get; set; } = [];

    // Réglages communs.
    public int CheckingDownDelaySeconds { get; set; } = 60;
    public int CheckingUpDelaySeconds { get; set; } = 30;
    public int FailuresBeforeDown { get; set; } = 3;
    public int HttpTimeoutSeconds { get; set; } = 10;
    public string TimeZoneId { get; set; } = "UTC";
}
