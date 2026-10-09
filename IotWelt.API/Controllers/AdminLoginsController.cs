using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.Common.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

// Login-Verwaltung für System-Admins (Epic E) — unabhängig von Konten und Konto-Rollen
[Authorize(Roles = Policies.AdminRole)]
[ApiController]
[Route("api/admin/logins")]
public class AdminLoginsController(
    AppDbContext db,
    UserManager<AppUser> users,
    CurrentAccount current,
    TokenService tokens,
    TimeProvider time) : ControllerBase
{
    // E1: alle Logins mit Konten und Rollen, sortiert nach E-Mail
    [HttpGet]
    public async Task<ActionResult<List<AdminLoginDto>>> GetAll()
    {
        var logins = await users.Users.OrderBy(u => u.Email).ToListAsync();

        var memberships = (await db.AccountMemberships
                .Include(m => m.Account)
                .OrderBy(m => m.JoinedAt)
                .ToListAsync())
            .ToLookup(m => m.UserId);

        var adminIds = (await users.GetUsersInRoleAsync(Policies.AdminRole))
            .Select(u => u.Id)
            .ToHashSet();

        var now = time.GetUtcNow();
        return logins
            .Select(u => new AdminLoginDto(
                u.Id,
                u.Email!,
                u.DisplayName,
                u.EmailConfirmed,
                adminIds.Contains(u.Id),
                u.LockoutEnd > now ? u.LockoutEnd : null,
                memberships[u.Id]
                    .Select(m => new AdminLoginAccountDto(m.Account.CustomerId, m.Account.Name, m.Role.ToString()))
                    .ToList()))
            .ToList();
    }

    // E2: unbefristet sperren. Anmeldung und Refresh scheitern ab sofort (Refresh prüft die Sperre);
    // alle Refresh-Tokens werden zusätzlich widerrufen. Ein Access-Token läuft nach max. 15 min ab.
    [HttpPost("{userId}/lock")]
    public async Task<IActionResult> Lock(string userId)
    {
        if (userId == current.UserId)
            return SelfAction();

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        // Ohne LockoutEnabled ignoriert Identity ein gesetztes LockoutEnd
        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await tokens.RevokeAllAsync(user.Id);
        return NoContent();
    }

    // E2: Sperre aufheben — auch eine automatische nach 5 Fehlversuchen
    [HttpPost("{userId}/unlock")]
    public async Task<IActionResult> Unlock(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return NotFound();

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return NoContent();
    }

    private ObjectResult SelfAction() =>
        Problem(title: AdminLoginErrors.SelfAction, statusCode: StatusCodes.Status409Conflict);
}
