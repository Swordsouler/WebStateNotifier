namespace WebStateNotifier.Models;

public class MonitorOptions
{
    public const string SectionName = "Monitor";

    public string Url { get; set; } = string.Empty;
    public List<string> Emails { get; set; } = [];
    public int CheckingDownDelaySeconds { get; set; } = 60;
    public int CheckingUpDelaySeconds { get; set; } = 30;
    public int HttpTimeoutSeconds { get; set; } = 10;
    public string TimeZoneId { get; set; } = "UTC";
}
