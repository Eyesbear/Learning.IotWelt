using IotWelt.API.Data;
using IotWelt.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IotWelt.API.Services;

// Einladungen in ein Konto (Epic B): Token erzeugen, Hash speichern, Link verschicken.
public class InvitationService(
    AppDbContext db,
    IInvitationEmailSender email,
    IOptions<AppLinkOptions> links,
    TimeProvider time)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    // E-Mail-Adressen werden kleingeschrieben gespeichert, damit Vergleiche und der Index
    // (AccountId, Email) unabhängig von der Schreibweise funktionieren.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // B1: Gibt es für die Adresse schon eine nicht angenommene Einladung, wird sie ersetzt —
    // neues Token, neue Laufzeit, ggf. neue Rolle. Der alte Link ist damit ungültig.
    public async Task<AccountInvitation> InviteAsync(Account account, string emailAddress, AccountRole role)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var normalized = NormalizeEmail(emailAddress);

        var invitation = await db.AccountInvitations
            .FirstOrDefaultAsync(i => i.AccountId == account.Id && i.Email == normalized && i.AcceptedAt == null);
        if (invitation is null)
        {
            invitation = new AccountInvitation { AccountId = account.Id, Email = normalized };
            db.AccountInvitations.Add(invitation);
        }

        var token = SecureToken.New();
        invitation.TokenHash = SecureToken.Hash(token);
        invitation.Role = role;
        invitation.CreatedAt = now;
        invitation.ExpiresAt = now.Add(Lifetime);
        await db.SaveChangesAsync();

        var link = $"{links.Value.PortalBaseUrl}/account/accept-invitation?token={token}";
        await email.SendInvitationLinkAsync(normalized, account.Name, role, link);
        return invitation;
    }
}
