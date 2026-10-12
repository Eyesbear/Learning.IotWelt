using System.Net;
using IotWelt.API.Models;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;

namespace IotWelt.API.Services;

// Verschickt Bestätigungs-, Reset- und Einladungsmails per SMTP (MailKit).
// Lokal und in Staging landet alles in Mailpit, in Produktion (Phase 4) beim SMTP-Server von myASP.NET.
public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    : IEmailSender<AppUser>, IInvitationEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) =>
        SendAsync(email, "IotWelt: E-Mail-Adresse bestätigen",
            "Bitte bestätigen Sie Ihre E-Mail-Adresse für IotWelt.", "Adresse bestätigen", confirmationLink);

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) =>
        SendAsync(email, "IotWelt: Passwort zurücksetzen",
            "Für Ihren IotWelt-Login wurde ein neues Passwort angefordert. Falls Sie das nicht waren, ignorieren Sie diese Mail.",
            "Neues Passwort festlegen", resetLink);

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) =>
        SendAsync(email, "IotWelt: Code zum Zurücksetzen",
            $"Ihr Code zum Zurücksetzen des Passworts: {resetCode}", linkText: null, link: null);

    public Task SendInvitationLinkAsync(string email, string accountName, AccountRole role, string invitationLink) =>
        SendAsync(email, $"IotWelt: Einladung in das Konto {accountName}",
            $"Sie wurden in das IotWelt-Konto „{accountName}“ eingeladen (Rolle: {role}). Der Link ist 7 Tage gültig.",
            "Einladung annehmen", invitationLink);

    private async Task SendAsync(string to, string subject, string text, string? linkText, string? link)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        // Text- und HTML-Teil; im HTML wird alles kodiert, auch der Link (enthält & aus der Query)
        var body = new BodyBuilder { TextBody = link is null ? text : $"{text}\n\n{linkText}: {link}" };
        var html = $"<p>{WebUtility.HtmlEncode(text)}</p>";
        if (link is not null)
            html += $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(linkText)}</a></p>";
        body.HtmlBody = html;
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, _options.Security);
        if (!string.IsNullOrEmpty(_options.User))
            await client.AuthenticateAsync(_options.User, _options.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);

        logger.LogInformation("E-Mail an {Email} verschickt: {Subject}", to, subject);
    }
}
