using IotWelt.API.Models;
using Microsoft.AspNetCore.Identity;

namespace IotWelt.API.Services;

// Platzhalter bis Phase 4 (SMTP bei myASP.NET): schreibt die Links ins Log statt sie zu versenden.
// Lokal im Aspire-Dashboard unter "Console logs" bzw. "Structured logs" der API zu finden.
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender<AppUser>, IInvitationEmailSender
{
    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
    {
        logger.LogInformation("E-Mail an {Email} — Adresse bestätigen: {Link}", email, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
    {
        logger.LogInformation("E-Mail an {Email} — Passwort zurücksetzen: {Link}", email, resetLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
    {
        logger.LogInformation("E-Mail an {Email} — Code zum Zurücksetzen: {Code}", email, resetCode);
        return Task.CompletedTask;
    }

    public Task SendInvitationLinkAsync(string email, string accountName, AccountRole role, string invitationLink)
    {
        logger.LogInformation("E-Mail an {Email} — Einladung in Konto {Account} als {Role}: {Link}",
            email, accountName, role, invitationLink);
        return Task.CompletedTask;
    }
}
