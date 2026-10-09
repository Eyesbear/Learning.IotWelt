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

    public Task<AccountInvitation?> FindByTokenAsync(string token)
    {
        var hash = SecureToken.Hash(token);
        return db.AccountInvitations
            .Include(i => i.Account)
            .FirstOrDefaultAsync(i => i.TokenHash == hash);
    }

    // B2/B3: Einladung einlösen und Mitgliedschaft vormerken (gespeichert wird mit dem nächsten SaveChanges).
    // Das Einlösen selbst ist ein bedingtes UPDATE direkt in der DB: Nur wer die Zeile mit
    // AcceptedAt = NULL erwischt, gewinnt. Zwei gleichzeitige Annahmen können so nicht beide durchgehen.
    // Läuft in der Transaktion des Aufrufers — scheitert danach etwas, wird das Einlösen zurückgerollt.
    public async Task<bool> TryClaimAsync(AccountInvitation invitation, AppUser user)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var claimed = await db.AccountInvitations
            .Where(i => i.Id == invitation.Id && i.AcceptedAt == null && i.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.AcceptedAt, now));
        if (claimed == 0)
            return false;

        db.AccountMemberships.Add(new AccountMembership
        {
            AccountId = invitation.AccountId,
            User = user,
            Role = invitation.Role,
            JoinedAt = now
        });
        return true;
    }
}
