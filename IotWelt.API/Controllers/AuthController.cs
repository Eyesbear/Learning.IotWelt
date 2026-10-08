using System.Text;
using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.Common.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IotWelt.API.Controllers;

// Registrierung, Anmeldung und Token-Verwaltung (Epic A + D der User Stories)
[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    AccountService accounts,
    TokenService tokens,
    CurrentAccount current,
    IEmailSender<AppUser> email,
    IOptions<AppLinkOptions> links) : ControllerBase
{
    // A1: Login + eigenes Konto (Owner) anlegen, Bestätigungslink verschicken
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        AppUser? created = null;

        // Login und Konto gemeinsam — scheitert eins, darf das andere nicht übrig bleiben.
        // Aspire aktiviert Retries (SqlServerRetryingExecutionStrategy); eigene Transaktionen
        // müssen deshalb über die Execution Strategy laufen, damit sie als Ganzes wiederholbar sind.
        var strategy = db.Database.CreateExecutionStrategy();
        var failure = await strategy.ExecuteAsync(async () =>
        {
            var user = new AppUser
            {
                UserName = request.Email,
                Email = request.Email,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim()
            };

            await using var tx = await db.Database.BeginTransactionAsync();

            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return result;

            await accounts.CreateOwnedAccountAsync(user, user.DisplayName ?? request.Email);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            created = user;
            return null;
        });

        if (failure is not null)
            return IdentityProblem(failure);

        await SendConfirmationAsync(created!);
        return Ok();
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
            return BadRequest();

        var result = await users.ConfirmEmailAsync(user, DecodeCode(request.Code));
        return result.Succeeded ? Ok() : IdentityProblem(result);
    }

    // A2: liefert Access- + Refresh-Token. Sperre nach 5 Fehlversuchen (Lockout), E-Mail muss bestätigt sein.
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
            return LoginFailed(AuthErrors.InvalidCredentials);

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return LoginFailed(AuthErrors.LockedOut);
        if (result.IsNotAllowed)
            return LoginFailed(AuthErrors.EmailNotConfirmed);
        if (!result.Succeeded)
            return LoginFailed(AuthErrors.InvalidCredentials);

        var membership = await accounts.ResolveActiveAsync(user);
        return await tokens.IssueAsync(user, membership);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request)
    {
        var issued = await tokens.RefreshAsync(request.RefreshToken);
        return issued is null ? LoginFailed(AuthErrors.InvalidRefreshToken) : issued;
    }

    // Widerruft das Refresh-Token; das Access-Token läuft nach max. 15 min von selbst ab
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        await tokens.RevokeAsync(request.RefreshToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var user = await users.FindByIdAsync(current.UserId!);
        if (user is null)
            return Unauthorized();

        return new MeResponse(
            user.Id,
            user.Email!,
            user.DisplayName,
            User.IsInRole(Policies.AdminRole),
            current.CustomerId,
            current.Role?.ToString());
    }

    // D1: alle Konten des Logins für den Kontowechsler
    [Authorize]
    [HttpGet("accounts")]
    public async Task<ActionResult<List<AccountSummaryDto>>> MyAccounts()
    {
        var memberships = await accounts.GetMembershipsAsync(current.UserId!);
        return memberships
            .Select(m => new AccountSummaryDto(
                m.Account.CustomerId, m.Account.Name, m.Role.ToString(),
                m.Account.CustomerId == current.CustomerId))
            .ToList();
    }

    // D2: neues Token-Paar im Kontext eines anderen Kontos; das alte Refresh-Token wird dabei verbraucht
    [Authorize]
    [HttpPost("switch-account")]
    public async Task<ActionResult<TokenResponse>> SwitchAccount(SwitchAccountRequest request)
    {
        var memberships = await accounts.GetMembershipsAsync(current.UserId!);
        var target = memberships.FirstOrDefault(m => m.Account.CustomerId == request.CustomerId);
        if (target is null)
            return Forbid();

        var issued = await tokens.RefreshAsync(request.RefreshToken, target.AccountId);
        return issued is null ? LoginFailed(AuthErrors.InvalidRefreshToken) : issued;
    }

    // A3: Antwort immer 200 — verrät nicht, ob die Adresse registriert ist
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null && await users.IsEmailConfirmedAsync(user))
        {
            var code = EncodeCode(await users.GeneratePasswordResetTokenAsync(user));
            var link = $"{links.Value.PortalBaseUrl}/account/reset-password?email={Uri.EscapeDataString(user.Email!)}&code={code}";
            await email.SendPasswordResetLinkAsync(user, user.Email!, link);
        }
        return Ok();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
            return BadRequest();

        var result = await users.ResetPasswordAsync(user, DecodeCode(request.Code), request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);

        await tokens.RevokeAllAsync(user.Id);   // alle Sitzungen beenden
        return Ok();
    }

    // A3: Nach der Änderung sind alle anderen Sitzungen beendet; die aktuelle bekommt ein neues Token-Paar
    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<TokenResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var user = await users.FindByIdAsync(current.UserId!);
        if (user is null)
            return Unauthorized();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);

        await tokens.RevokeAllAsync(user.Id);
        var membership = await accounts.ResolveActiveAsync(user);
        return await tokens.IssueAsync(user, membership);
    }

    private async Task SendConfirmationAsync(AppUser user)
    {
        var code = EncodeCode(await users.GenerateEmailConfirmationTokenAsync(user));
        var link = $"{links.Value.PortalBaseUrl}/account/confirm-email?userId={user.Id}&code={code}";
        await email.SendConfirmationLinkAsync(user, user.Email!, link);
    }

    // Identity-Tokens enthalten +, / und = — für URLs Base64Url-kodieren
    private static string EncodeCode(string code) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

    private static string DecodeCode(string code)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); }
        catch (FormatException) { return string.Empty; }   // ungültiger Code → Identity meldet "InvalidToken"
    }

    private ObjectResult LoginFailed(string error) =>
        Problem(title: error, statusCode: StatusCodes.Status401Unauthorized);

    private ActionResult IdentityProblem(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(error.Code, error.Description);
        return ValidationProblem(ModelState);
    }
}
