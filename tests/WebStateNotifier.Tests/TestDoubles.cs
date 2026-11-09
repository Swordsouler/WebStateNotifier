using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using WebStateNotifier.Models;
using WebStateNotifier.Services;

namespace WebStateNotifier.Tests;

/// <summary>Réponses réglables site par site ; un site non réglé répond.</summary>
internal sealed class FakeHealthChecker : IUrlHealthChecker
{
    private readonly Dictionary<string, bool> _isUp = [];

    public List<string> CheckedUrls { get; } = [];

    public void SetUp(string url) => _isUp[url] = true;
    public void SetDown(string url) => _isUp[url] = false;

    public Task<(bool IsUp, int? StatusCode, string? Error)> CheckAsync(string url, CancellationToken ct)
    {
        CheckedUrls.Add(url);
        (bool IsUp, int? StatusCode, string? Error) result = _isUp.GetValueOrDefault(url, true)
            ? (true, 200, null)
            : (false, 503, "HTTP 503");
        return Task.FromResult(result);
    }
}

internal sealed record SentNotification(string Kind, SiteMonitor Monitor, DateTimeOffset At, TimeSpan? Downtime);

/// <summary>Note les notifications demandées, sans rien envoyer.</summary>
internal sealed class RecordingEmailService : IEmailService
{
    public List<SentNotification> Sent { get; } = [];

    public Task SendDownNotificationAsync(SiteMonitor monitor, DateTimeOffset detectedAt, CancellationToken ct)
    {
        Sent.Add(new SentNotification("down", monitor, detectedAt, null));
        return Task.CompletedTask;
    }

    public Task SendUpNotificationAsync(SiteMonitor monitor, DateTimeOffset detectedAt, TimeSpan downtime, CancellationToken ct)
    {
        Sent.Add(new SentNotification("up", monitor, detectedAt, downtime));
        return Task.CompletedTask;
    }
}

/// <summary>Le vrai EmailService, dont on garde les messages construits au lieu de les remettre au SMTP.</summary>
internal sealed class CapturingEmailService() : EmailService(
    Options.Create(new SmtpOptions { FromAddress = "notifier@example.com" }),
    Options.Create(new MonitorOptions()),
    NullLogger<EmailService>.Instance)
{
    public List<MimeMessage> Messages { get; } = [];

    protected override Task DeliverAsync(MimeMessage message, CancellationToken ct)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan duration) => Now += duration;
}
