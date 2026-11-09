using WebStateNotifier.Models;
using WebStateNotifier.Services;
using WebStateNotifier.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MonitorOptions>(
    builder.Configuration.GetSection(MonitorOptions.SectionName));

builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));

builder.Services.AddHttpClient("monitor", (sp, client) =>
{
    var options = sp.GetRequiredService<IConfiguration>()
        .GetSection(MonitorOptions.SectionName)
        .Get<MonitorOptions>() ?? new MonitorOptions();

    client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WebStateNotifier/1.0");
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IUrlHealthChecker, UrlHealthChecker>();
builder.Services.AddHostedService<MonitorWorker>();

var host = builder.Build();
host.Run();
