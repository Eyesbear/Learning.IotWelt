using System.Net.Mail;
using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.Common.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

// Mitglieder und Einladungen des aktiven Kontos (Epic B + C) — nur für den Owner
[Authorize(Policy = Policies.IsOwner)]
[ApiController]
[Route("api/members")]
public class MembersController(
    AppDbContext db,
    CurrentAccount current,
    UserManager<AppUser> users,
    InvitationService invitations,
    TimeProvider time) : ControllerBase
{
    // C1: Mitglieder (Owner zuerst) und Einladungen, die noch nicht angenommen wurden
    [HttpGet]
    public async Task<ActionResult<MembersOverviewDto>> Get()
    {
        var account = await ActiveAccountAsync();
        if (account is null)
            return NotFound();

        var memberships = await db.AccountMemberships
            .Where(m => m.AccountId == account.Id)
            .Include(m => m.User)
            .ToListAsync();

        // Sortieren erst im Speicher: Role ist in der DB Text, dort wäre die Reihenfolge alphabetisch
        var members = memberships
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new MemberDto(m.UserId, m.User.Email!, m.User.DisplayName, m.Role.ToString(), m.JoinedAt))
            .ToList();

        var open = await db.AccountInvitations
            .Where(i => i.AccountId == account.Id && i.AcceptedAt == null)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();

        return new MembersOverviewDto(members, open.Select(ToDto).ToList());
    }

    // B1: Einladung per E-Mail mit Rolle Editor oder Reader
    [HttpPost("invitations")]
    public async Task<ActionResult<InvitationDto>> Invite(InviteMemberRequest request)
    {
        var email = InvitationService.NormalizeEmail(request.Email ?? string.Empty);
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
            ModelState.AddModelError(nameof(request.Email), "Keine gültige E-Mail-Adresse.");
        if (!TryParseMemberRole(request.Role, out var role))
            ModelState.AddModelError(nameof(request.Role), "Erlaubt sind nur Editor und Reader.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var account = await ActiveAccountAsync();
        if (account is null)
            return NotFound();

        var identityEmail = users.NormalizeEmail(email);
        if (await db.AccountMemberships.AnyAsync(m => m.AccountId == account.Id && m.User.NormalizedEmail == identityEmail))
            return Problem(title: MemberErrors.AlreadyMember, statusCode: StatusCodes.Status409Conflict);

        var invitation = await invitations.InviteAsync(account, email, role);
        return ToDto(invitation);
    }

    // B1: offene Einladung zurückziehen — der Link ist danach ungültig
    [HttpDelete("invitations/{id:int}")]
    public async Task<IActionResult> Revoke(int id)
    {
        var invitation = await db.AccountInvitations
            .FirstOrDefaultAsync(i => i.Id == id && i.Account.CustomerId == current.CustomerId && i.AcceptedAt == null);
        if (invitation is null)
            return NotFound();

        db.AccountInvitations.Remove(invitation);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // C2: Editor ↔ Reader. Wirkt beim nächsten Token-Refresh des Mitglieds (Rolle wird dort frisch gelesen).
    [HttpPut("{userId}/role")]
    public async Task<IActionResult> ChangeRole(string userId, ChangeMemberRoleRequest request)
    {
        if (!TryParseMemberRole(request.Role, out var role))
        {
            ModelState.AddModelError(nameof(request.Role), "Erlaubt sind nur Editor und Reader.");
            return ValidationProblem(ModelState);
        }

        var membership = await FindMembershipAsync(userId);
        if (membership is null)
            return NotFound();
        if (membership.Role == AccountRole.Owner)
            return OwnerMembership();

        membership.Role = role;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // C3: Zugriff entziehen. Das Mitglied sieht das Konto spätestens nach dem nächsten Refresh nicht mehr.
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(string userId)
    {
        var membership = await FindMembershipAsync(userId);
        if (membership is null)
            return NotFound();
        if (membership.Role == AccountRole.Owner)
            return OwnerMembership();

        db.AccountMemberships.Remove(membership);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // C5: Ownership an ein bestehendes Mitglied übertragen; der bisherige Owner wird Editor.
    // Das eigene Access-Token sagt danach noch bis zu 15 min "Owner" — der Client sollte sofort refreshen.
    [HttpPost("transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(TransferOwnershipRequest request)
    {
        var target = await FindMembershipAsync(request.UserId);
        if (target is null)
            return NotFound();
        if (target.Role == AccountRole.Owner)
        {
            ModelState.AddModelError(nameof(request.UserId), "Dieses Mitglied ist bereits Owner.");
            return ValidationProblem(ModelState);
        }

        // Zwei bedingte UPDATEs in einer Transaktion, Reihenfolge wichtig: erst abgeben, dann übernehmen —
        // sonst gäbe es kurz zwei Owner und der Index IX_AccountMemberships_OneOwnerPerAccount schlägt an.
        // Die Bedingung "Role = Owner" verhindert, dass zwei gleichzeitige Übertragungen beide durchgehen.
        var strategy = db.Database.CreateExecutionStrategy();
        var transferred = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            var demoted = await db.AccountMemberships
                .Where(m => m.AccountId == target.AccountId && m.UserId == current.UserId && m.Role == AccountRole.Owner)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, AccountRole.Editor));
            if (demoted == 0)
                return false;   // Aufrufer ist (nicht mehr) Owner — gar nicht erst einen neuen ernennen

            var promoted = await db.AccountMemberships
                .Where(m => m.Id == target.Id && m.Role != AccountRole.Owner)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, AccountRole.Owner));
            if (promoted == 0)
                return false;   // Ziel inzwischen entfernt — ohne Commit → Rollback, alter Owner bleibt

            await tx.CommitAsync();
            return true;
        });

        return transferred
            ? NoContent()
            : Problem(title: MemberErrors.NotOwner, statusCode: StatusCodes.Status409Conflict);
    }

    // Nur die Rollennamen Editor/Reader (Groß-/Kleinschreibung egal) — keine Zahlen wie "1", kein Owner
    private static bool TryParseMemberRole(string? value, out AccountRole role) =>
        Enum.TryParse(value, ignoreCase: true, out role)
        && string.Equals(role.ToString(), value, StringComparison.OrdinalIgnoreCase)
        && role is AccountRole.Editor or AccountRole.Reader;

    private Task<AccountMembership?> FindMembershipAsync(string userId) =>
        db.AccountMemberships.FirstOrDefaultAsync(m => m.UserId == userId && m.Account.CustomerId == current.CustomerId);

    // Der Owner bleibt Owner, bis er die Ownership überträgt (C5) — so hat jedes Konto immer genau einen
    private ObjectResult OwnerMembership() =>
        Problem(title: MemberErrors.OwnerMembership, statusCode: StatusCodes.Status409Conflict);

    private Task<Account?> ActiveAccountAsync() =>
        db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == current.CustomerId);

    private InvitationDto ToDto(AccountInvitation i) =>
        new(i.Id, i.Email, i.Role.ToString(), i.CreatedAt, i.ExpiresAt, !i.IsPending(time.GetUtcNow().UtcDateTime));
}
