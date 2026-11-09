using Microsoft.Extensions.Configuration;
using WebStateNotifier.Services;

namespace WebStateNotifier.Tests;

public class MonitorConfigurationTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void NewFormat_GivesEachMonitorItsOwnNameUrlRecipientsAndThreshold()
    {
        var monitors = MonitorConfiguration.Load(Configuration(
            ("Monitor:FailuresBeforeDown", "3"),
            ("Monitors:0:Name", "StreamBlades"),
            ("Monitors:0:Url", "https://streamblades.com/health"),
            ("Monitors:0:Recipients:0", "owner@example.com"),
            ("Monitors:1:Name", "Autre site"),
            ("Monitors:1:Url", "https://autre.example.org"),
            ("Monitors:1:Recipients:0", "a@example.org"),
            ("Monitors:1:Recipients:1", "b@example.org"),
            ("Monitors:1:FailuresBeforeDown", "5")));

        Assert.Collection(monitors,
            streamBlades =>
            {
                Assert.Equal("StreamBlades", streamBlades.Name);
                Assert.Equal("https://streamblades.com/health", streamBlades.Url);
                Assert.Equal(new[] { "owner@example.com" }, streamBlades.Recipients);
                Assert.Equal(3, streamBlades.FailuresBeforeDown);
            },
            other =>
            {
                Assert.Equal("Autre site", other.Name);
                Assert.Equal("https://autre.example.org", other.Url);
                Assert.Equal(new[] { "a@example.org", "b@example.org" }, other.Recipients);
                Assert.Equal(5, other.FailuresBeforeDown);
            });
    }

    [Fact]
    public void NewFormat_ReadsTheDoubleUnderscoreVariablesOfTheEnvFile()
    {
        var prefix = $"WSN_TEST_{Guid.NewGuid():N}_";
        var variables = new Dictionary<string, string>
        {
            ["Monitor__FailuresBeforeDown"] = "3",
            ["Monitors__0__Name"] = "StreamBlades",
            ["Monitors__0__Url"] = "https://streamblades.com/health",
            ["Monitors__0__Recipients__0"] = "owner@example.com",
            ["Monitors__0__FailuresBeforeDown"] = "2",
        };

        try
        {
            foreach (var (name, value) in variables)
                Environment.SetEnvironmentVariable(prefix + name, value);

            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();

            var monitor = Assert.Single(MonitorConfiguration.Load(configuration));
            Assert.Equal("StreamBlades", monitor.Name);
            Assert.Equal("https://streamblades.com/health", monitor.Url);
            Assert.Equal(new[] { "owner@example.com" }, monitor.Recipients);
            Assert.Equal(2, monitor.FailuresBeforeDown);
        }
        finally
        {
            foreach (var name in variables.Keys)
                Environment.SetEnvironmentVariable(prefix + name, null);
        }
    }

    [Fact]
    public void LegacyFormat_StillWorksAsASingleMonitor()
    {
        var monitors = MonitorConfiguration.Load(Configuration(
            ("Monitor:Url", "https://legacy.example.org"),
            ("Monitor:Emails:0", "one@example.org"),
            ("Monitor:Emails:1", "two@example.org"),
            ("Monitor:Emails:2", "three@example.org"),
            ("Monitor:Emails:3", "four@example.org"),
            ("Monitor:Emails:4", "five@example.org"),
            ("Monitor:FailuresBeforeDown", "3")));

        var monitor = Assert.Single(monitors);
        Assert.Equal("legacy.example.org", monitor.Name);
        Assert.Equal("https://legacy.example.org", monitor.Url);
        Assert.Equal(
            new[] { "one@example.org", "two@example.org", "three@example.org", "four@example.org", "five@example.org" },
            monitor.Recipients);
        Assert.Equal(3, monitor.FailuresBeforeDown);
    }

    [Fact]
    public void LegacyFormat_CanBeNamed()
    {
        var monitor = Assert.Single(MonitorConfiguration.Load(Configuration(
            ("Monitor:Name", "Site historique"),
            ("Monitor:Url", "https://legacy.example.org"),
            ("Monitor:Emails:0", "one@example.org"))));

        Assert.Equal("Site historique", monitor.Name);
    }

    [Fact]
    public void LegacyAndNewFormats_AreBothMonitoredWithTheirOwnRecipients()
    {
        var monitors = MonitorConfiguration.Load(Configuration(
            ("Monitor:Url", "https://legacy.example.org"),
            ("Monitor:Emails:0", "team@example.org"),
            ("Monitors:0:Name", "StreamBlades"),
            ("Monitors:0:Url", "https://streamblades.com/"),
            ("Monitors:0:Recipients:0", "owner@example.com")));

        Assert.Collection(monitors,
            legacy =>
            {
                Assert.Equal("https://legacy.example.org", legacy.Url);
                Assert.Equal(new[] { "team@example.org" }, legacy.Recipients);
            },
            streamBlades =>
            {
                Assert.Equal("https://streamblades.com/", streamBlades.Url);
                Assert.Equal(new[] { "owner@example.com" }, streamBlades.Recipients);
            });
    }

    [Fact]
    public void MissingName_DefaultsToTheUrlHost()
    {
        var monitor = Assert.Single(MonitorConfiguration.Load(Configuration(
            ("Monitors:0:Url", "https://streamblades.com/health"),
            ("Monitors:0:Recipients:0", "owner@example.com"))));

        Assert.Equal("streamblades.com", monitor.Name);
    }

    [Fact]
    public void ShippedAppSettings_DoNotAddAMonitorOfTheirOwn()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitors:0:Url"] = "https://streamblades.com/health",
                ["Monitors:0:Recipients:0"] = "owner@example.com",
            })
            .Build();

        var monitor = Assert.Single(MonitorConfiguration.Load(configuration));
        Assert.Equal("https://streamblades.com/health", monitor.Url);
    }

    [Fact]
    public void EnvExample_IsAValidConfigurationWithTheStreamBladesExample()
    {
        var values = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "env.example"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2))
            .Select(parts => (parts[0].Replace("__", ConfigurationPath.KeyDelimiter), parts[1]))
            .ToArray();

        var monitors = MonitorConfiguration.Load(Configuration(values));

        var streamBlades = Assert.Single(monitors, m => m.Name == "StreamBlades");
        Assert.Equal("https://streamblades.com/health", streamBlades.Url);
        Assert.Equal(new[] { "alertes@example.com" }, streamBlades.Recipients);
        Assert.Equal(2, monitors.Count);
    }

    [Fact]
    public void NoMonitorAtAll_IsRejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() => MonitorConfiguration.Load(Configuration()));
        Assert.Contains("Monitors__0__Url", error.Message);
    }

    [Theory]
    [InlineData("Monitors:0:Url", "", "Monitors__0__Url")]
    [InlineData("Monitors:0:Url", "streamblades.com", "Monitors__0__Url")]
    [InlineData("Monitors:0:Url", "ftp://streamblades.com", "Monitors__0__Url")]
    [InlineData("Monitors:0:Recipients:0", "", "Monitors__0__Recipients")]
    [InlineData("Monitors:0:Recipients:0", "owner", "Monitors__0__Recipients")]
    [InlineData("Monitors:0:Recipients:0", "owner＠example.com", "Monitors__0__Recipients")]
    [InlineData("Monitors:0:FailuresBeforeDown", "0", "Monitors__0__FailuresBeforeDown")]
    [InlineData("Monitor:FailuresBeforeDown", "0", "Monitor__FailuresBeforeDown")]
    public void InvalidMonitor_IsRejectedWithTheVariableToFix(string key, string value, string expectedVariable)
    {
        var values = new Dictionary<string, string>
        {
            ["Monitors:0:Url"] = "https://streamblades.com/health",
            ["Monitors:0:Recipients:0"] = "owner@example.com",
            [key] = value,
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => MonitorConfiguration.Load(Configuration(values.Select(v => (v.Key, v.Value)).ToArray())));
        Assert.Contains(expectedVariable, error.Message);
    }
}
