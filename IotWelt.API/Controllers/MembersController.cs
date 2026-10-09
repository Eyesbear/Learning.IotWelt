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

    // Nur die Rollennamen Editor/Reader (Groß-/Kleinschreibung egal) — keine Zahlen wie "1", kein Owner
    private static bool TryParseMemberRole(string? value, out AccountRole role) =>
        Enum.TryParse(value, ignoreCase: true, out role)
        && string.Equals(role.ToString(), value, StringComparison.OrdinalIgnoreCase)
        && role is AccountRole.Editor or AccountRole.Reader;

    private Task<Account?> ActiveAccountAsync() =>
        db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == current.CustomerId);

    private InvitationDto ToDto(AccountInvitation i) =>
        new(i.Id, i.Email, i.Role.ToString(), i.CreatedAt, i.ExpiresAt, !i.IsPending(time.GetUtcNow().UtcDateTime));
}
