using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.Common.Auth;
using IotWelt.Common.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

// Einladungslink einlösen (Epic B). Das Token im Pfad ist der einzige Ausweis — wer den Link hat,
// sieht die Infos; annehmen kann ihn aber nur der Login mit der eingeladenen E-Mail-Adresse.
[ApiController]
[Route("api/invitations")]
public class InvitationsController(
    AppDbContext db,
    UserManager<AppUser> users,
    CurrentAccount current,
    InvitationService invitations,
    AccountService accounts,
    TokenService tokens,
    TimeProvider time) : ControllerBase
{
    // Infos für die Annahmeseite im Portal
    [HttpGet("{token}")]
    public async Task<ActionResult<InvitationInfoDto>> Get(string token)
    {
        var invitation = await invitations.FindByTokenAsync(token);
        if (invitation is null)
            return NotFound();

        var status = invitation.AcceptedAt is not null ? InvitationStatus.Accepted
            : invitation.IsPending(time.GetUtcNow().UtcDateTime) ? InvitationStatus.Open
            : InvitationStatus.Expired;
        var hasLogin = await users.FindByEmailAsync(invitation.Email) is not null;

        return new InvitationInfoDto(
            invitation.Account.Name, invitation.Email, invitation.Role.ToString(),
            invitation.ExpiresAt, status, hasLogin);
    }

    // B3: angemeldeter Login nimmt an — das Konto erscheint danach im Kontowechsler.
    // Ein neues Token-Paar gibt es hier bewusst nicht: In das Konto wechselt der Client per switch-account.
    [Authorize]
    [HttpPost("{token}/accept")]
    public async Task<ActionResult<AccountSummaryDto>> Accept(string token)
    {
        var invitation = await invitations.FindByTokenAsync(token);
        if (invitation is null)
            return NotFound();
        if (!invitation.IsPending(time.GetUtcNow().UtcDateTime))
            return InvitationInvalid();

        var user = await users.FindByIdAsync(current.UserId!);
        if (user is null)
            return Unauthorized();

        // Die Einladung gilt der Person, nicht dem Link: Ein weitergeleiteter Link nützt anderen nichts
        if (user.NormalizedEmail != users.NormalizeEmail(invitation.Email))
            return Problem(title: MemberErrors.InvitationEmailMismatch, statusCode: StatusCodes.Status403Forbidden);

        if (await db.AccountMemberships.AnyAsync(m => m.AccountId == invitation.AccountId && m.UserId == user.Id))
            return Problem(title: MemberErrors.AlreadyMember, statusCode: StatusCodes.Status409Conflict);

        // Einlösen + Mitgliedschaft gemeinsam; Retries von Aspire verlangen die Execution Strategy
        var strategy = db.Database.CreateExecutionStrategy();
        var claimed = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!await invitations.TryClaimAsync(invitation, user))
                return false;

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        });
        if (!claimed)
            return InvitationInvalid();   // zwischen Prüfung und Einlösen abgelaufen oder von anderer Anfrage verbraucht

        return new AccountSummaryDto(invitation.Account.CustomerId, invitation.Account.Name, invitation.Role.ToString(), IsActive: false);
    }

    // B2: neue Person legt über den Link einen Login an und ist danach Mitglied — ohne eigenes Konto.
    // Die E-Mail gilt als bestätigt (der Link kam an diese Adresse). Antwort: Token-Paar im eingeladenen Konto.
    [HttpPost("{token}/register")]
    public async Task<ActionResult<TokenResponse>> Register(string token, RegisterFromInvitationRequest request)
    {
        var invitation = await invitations.FindByTokenAsync(token);
        if (invitation is null)
            return NotFound();
        if (!invitation.IsPending(time.GetUtcNow().UtcDateTime))
            return InvitationInvalid();
        if (await users.FindByEmailAsync(invitation.Email) is not null)
            return Problem(title: MemberErrors.LoginExists, statusCode: StatusCodes.Status409Conflict);

        var user = new AppUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim()
        };

        // Login, Einlösen und Mitgliedschaft gemeinsam — scheitert eins (z. B. Passwortregeln),
        // bleibt weder ein Login ohne Konto noch eine verbrauchte Einladung übrig
        var strategy = db.Database.CreateExecutionStrategy();
        var (failure, claimed) = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return (result, false);
            if (!await invitations.TryClaimAsync(invitation, user))
                return (null, false);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return ((IdentityResult?)null, true);
        });

        if (failure is not null)
            return IdentityProblem(failure);
        if (!claimed)
            return InvitationInvalid();

        var membership = await accounts.ResolveActiveAsync(user, invitation.AccountId);
        return await tokens.IssueAsync(user, membership);
    }

    private ActionResult IdentityProblem(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(error.Code, error.Description);
        return ValidationProblem(ModelState);
    }

    private ObjectResult InvitationInvalid() =>
        Problem(title: MemberErrors.InvitationInvalid, statusCode: StatusCodes.Status410Gone);
}
