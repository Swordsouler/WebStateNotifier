using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using WebStateNotifier.Models;

namespace WebStateNotifier.Services;

public class EmailService(IOptions<SmtpOptions> smtpOptions, ILogger<EmailService> logger)
{
    private readonly SmtpOptions _smtp = smtpOptions.Value;

    public async Task SendDownNotificationAsync(string url, IEnumerable<string> recipients, DateTimeOffset detectedAt, CancellationToken ct)
    {
        var subject = $"[ALERTE] {url} est inaccessible";
        var body = BuildBody(
            title: "Service inaccessible",
            color: "#e74c3c",
            icon: "✖",
            message: $"Le service <strong>{url}</strong> ne répond plus.",
            detail: $"Panne détectée le : <strong>{detectedAt:dd/MM/yyyy à HH:mm:ss} (UTC)</strong>"
        );

        await SendAsync(subject, body, recipients, ct);
    }

    public async Task SendUpNotificationAsync(string url, IEnumerable<string> recipients, DateTimeOffset detectedAt, TimeSpan downtime, CancellationToken ct)
    {
        var subject = $"[RÉTABLI] {url} est de nouveau accessible";
        var body = BuildBody(
            title: "Service rétabli",
            color: "#27ae60",
            icon: "✔",
            message: $"Le service <strong>{url}</strong> est de nouveau opérationnel.",
            detail: $"Rétabli le : <strong>{detectedAt:dd/MM/yyyy à HH:mm:ss} (UTC)</strong><br/>Durée de l'indisponibilité : <strong>{FormatDuration(downtime)}</strong>"
        );

        await SendAsync(subject, body, recipients, ct);
    }

    private async Task SendAsync(string subject, string htmlBody, IEnumerable<string> recipients, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromAddress));

        foreach (var address in recipients)
            message.To.Add(MailboxAddress.Parse(address));

        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        try
        {
            var secureSocketOptions = _smtp.UseSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(_smtp.Host, _smtp.Port, secureSocketOptions, ct);

            if (!string.IsNullOrEmpty(_smtp.Username))
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password, ct);

            await client.SendAsync(message, ct);
            logger.LogInformation("Email '{Subject}' envoyé à {Count} destinataire(s)", subject, message.To.Count);
        }
        finally
        {
            await client.DisconnectAsync(true, ct);
        }
    }

    private static string BuildBody(string title, string color, string icon, string message, string detail) => $"""
        <!DOCTYPE html>
        <html lang="fr">
        <head><meta charset="utf-8"/></head>
        <body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0" style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.1)">
                <tr>
                  <td style="background:{color};padding:30px;text-align:center">
                    <div style="font-size:48px;line-height:1">{icon}</div>
                    <h1 style="color:#fff;margin:12px 0 0;font-size:24px">{title}</h1>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px 40px">
                    <p style="font-size:16px;color:#333;line-height:1.6">{message}</p>
                    <p style="font-size:14px;color:#666;line-height:1.8">{detail}</p>
                    <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
                    <p style="font-size:12px;color:#aaa;text-align:center">WebStateNotifier — surveillance automatique</p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 60) return $"{(int)ts.TotalSeconds} seconde(s)";
        if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes} minute(s) et {ts.Seconds} seconde(s)";
        return $"{(int)ts.TotalHours}h {ts.Minutes}min {ts.Seconds}s";
    }
}
