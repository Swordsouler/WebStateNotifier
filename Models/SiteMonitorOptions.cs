namespace WebStateNotifier.Models;

/// <summary>
/// Un site de la section « Monitors » : Monitors__0__Url, Monitors__0__Recipients__0…
/// </summary>
public class SiteMonitorOptions
{
    public const string SectionName = "Monitors";

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = [];

    // Vide : la valeur commune Monitor__FailuresBeforeDown s'applique.
    public int? FailuresBeforeDown { get; set; }
}
