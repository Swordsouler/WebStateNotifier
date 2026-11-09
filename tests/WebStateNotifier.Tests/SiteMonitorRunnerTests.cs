using Microsoft.Extensions.Logging.Abstractions;
using WebStateNotifier.Models;
using WebStateNotifier.Services;
using WebStateNotifier.Workers;

namespace WebStateNotifier.Tests;

public class SiteMonitorRunnerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly MonitorOptions Settings = new() { CheckingDownDelaySeconds = 60, CheckingUpDelaySeconds = 30 };

    private static readonly SiteMonitor StreamBlades =
        new("StreamBlades", "https://streamblades.com/health", ["owner@example.com"], FailuresBeforeDown: 3);

    private static readonly SiteMonitor OtherSite =
        new("Autre site", "https://autre.example.org", ["a@example.org", "b@example.org"], FailuresBeforeDown: 3);

    private readonly FakeHealthChecker _checker = new();
    private readonly RecordingEmailService _emails = new();
    private readonly ManualTimeProvider _time = new(Start);

    private SiteMonitorRunner Runner(SiteMonitor monitor, IEmailService? emailService = null) =>
        new(monitor, Settings, _checker, emailService ?? _emails, _time, NullLogger.Instance);

    [Fact]
    public async Task Alert_IsSentOnlyAfterFailuresBeforeDownFailuresInARow()
    {
        var runner = Runner(StreamBlades);
        _checker.SetDown(StreamBlades.Url);

        await runner.CheckOnceAsync(CancellationToken.None);
        await runner.CheckOnceAsync(CancellationToken.None);

        Assert.Equal(2, runner.ConsecutiveFailures);
        Assert.False(runner.IsDown);
        Assert.Empty(_emails.Sent);

        await runner.CheckOnceAsync(CancellationToken.None);

        Assert.True(runner.IsDown);
        Assert.Equal("down", Assert.Single(_emails.Sent).Kind);

        await runner.CheckOnceAsync(CancellationToken.None);

        Assert.Single(_emails.Sent);
    }

    [Fact]
    public async Task SuccessBeforeTheThreshold_ResetsTheCounterWithoutAnyEmail()
    {
        var runner = Runner(StreamBlades);

        _checker.SetDown(StreamBlades.Url);
        await runner.CheckOnceAsync(CancellationToken.None);
        await runner.CheckOnceAsync(CancellationToken.None);
        _checker.SetUp(StreamBlades.Url);
        await runner.CheckOnceAsync(CancellationToken.None);

        Assert.Equal(0, runner.ConsecutiveFailures);

        _checker.SetDown(StreamBlades.Url);
        await runner.CheckOnceAsync(CancellationToken.None);
        await runner.CheckOnceAsync(CancellationToken.None);

        Assert.False(runner.IsDown);
        Assert.Empty(_emails.Sent);
    }

    [Fact]
    public async Task Recovery_SendsTheUpEmailWithTheDowntimeSinceTheFirstFailure()
    {
        var runner = Runner(StreamBlades);
        _checker.SetDown(StreamBlades.Url);

        for (var i = 0; i < 3; i++)
        {
            await runner.CheckOnceAsync(CancellationToken.None);
            _time.Advance(TimeSpan.FromSeconds(30));
        }

        _checker.SetUp(StreamBlades.Url);
        await runner.CheckOnceAsync(CancellationToken.None);

        Assert.Collection(_emails.Sent,
            down =>
            {
                Assert.Equal("down", down.Kind);
                Assert.Equal(Start, down.At);
            },
            up =>
            {
                Assert.Equal("up", up.Kind);
                Assert.Equal(TimeSpan.FromSeconds(90), up.Downtime);
            });
        Assert.False(runner.IsDown);
        Assert.Equal(0, runner.ConsecutiveFailures);
    }

    [Fact]
    public async Task EachMonitor_CountsItsOwnFailures()
    {
        var streamBlades = Runner(StreamBlades);
        var other = Runner(OtherSite);
        _checker.SetDown(StreamBlades.Url);
        _checker.SetDown(OtherSite.Url);

        await streamBlades.CheckOnceAsync(CancellationToken.None);
        await streamBlades.CheckOnceAsync(CancellationToken.None);
        await other.CheckOnceAsync(CancellationToken.None);

        Assert.Equal(2, streamBlades.ConsecutiveFailures);
        Assert.Equal(1, other.ConsecutiveFailures);

        _checker.SetUp(OtherSite.Url);
        await other.CheckOnceAsync(CancellationToken.None);
        await streamBlades.CheckOnceAsync(CancellationToken.None);

        Assert.Equal(0, other.ConsecutiveFailures);
        Assert.False(other.IsDown);
        Assert.True(streamBlades.IsDown);
        Assert.All(_emails.Sent, sent => Assert.Equal(StreamBlades, sent.Monitor));
    }

    [Fact]
    public async Task FailuresBeforeDown_IsReadPerMonitor()
    {
        var impatient = Runner(OtherSite with { FailuresBeforeDown = 1 });
        var patient = Runner(StreamBlades with { FailuresBeforeDown = 3 });
        _checker.SetDown(OtherSite.Url);
        _checker.SetDown(StreamBlades.Url);

        await impatient.CheckOnceAsync(CancellationToken.None);
        await patient.CheckOnceAsync(CancellationToken.None);

        Assert.True(impatient.IsDown);
        Assert.False(patient.IsDown);

        await patient.CheckOnceAsync(CancellationToken.None);
        await patient.CheckOnceAsync(CancellationToken.None);

        Assert.True(patient.IsDown);
        Assert.Equal(2, _emails.Sent.Count);
    }

    [Fact]
    public async Task Outage_IsEmailedOnlyToTheRecipientsOfTheFailingSite()
    {
        var emailService = new CapturingEmailService();
        var streamBlades = Runner(StreamBlades, emailService);
        var other = Runner(OtherSite, emailService);
        _checker.SetDown(StreamBlades.Url);

        for (var i = 0; i < 3; i++)
        {
            await streamBlades.CheckOnceAsync(CancellationToken.None);
            await other.CheckOnceAsync(CancellationToken.None);
        }

        _checker.SetUp(StreamBlades.Url);
        await streamBlades.CheckOnceAsync(CancellationToken.None);

        Assert.Equal(2, emailService.Messages.Count);
        Assert.All(emailService.Messages, message =>
        {
            Assert.Equal(new[] { "owner@example.com" }, message.To.Mailboxes.Select(m => m.Address));
            Assert.Empty(message.Cc);
            Assert.Empty(message.Bcc);
            Assert.Contains("StreamBlades", message.Subject);
        });
        Assert.StartsWith("[ALERTE] StreamBlades", emailService.Messages[0].Subject);
        Assert.StartsWith("[RÉTABLI] StreamBlades", emailService.Messages[1].Subject);
        Assert.Contains("https://streamblades.com/health", emailService.Messages[0].HtmlBody);
    }

    [Fact]
    public async Task AFailure_IsRecheckedAtTheOutagePace()
    {
        var runner = Runner(StreamBlades);
        Assert.Equal(TimeSpan.FromSeconds(60), runner.NextDelay);

        _checker.SetDown(StreamBlades.Url);
        await runner.CheckOnceAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(30), runner.NextDelay);
    }
}
