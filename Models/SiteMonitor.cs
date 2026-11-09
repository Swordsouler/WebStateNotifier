namespace WebStateNotifier.Models;

/// <summary>
/// Un site à surveiller, validé : son nom affiché, son URL, le groupe prévenu de ses pannes
/// et le nombre d'échecs de suite qui déclenche l'alerte.
/// </summary>
public sealed record SiteMonitor(string Name, string Url, IReadOnlyList<string> Recipients, int FailuresBeforeDown);
