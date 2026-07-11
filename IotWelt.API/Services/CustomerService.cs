using IotWelt.API.Data;
using IotWelt.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace IotWelt.API.Services;

public class CustomerService(AppDbContext db)
{
    // Stabiler Benutzerbezeichner aus dem JWT — oid bevorzugt, sub als Fallback
    public static string? GetOid(ClaimsPrincipal user) =>
        user.FindFirstValue("oid") ??
        user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier") ??
        user.FindFirstValue("sub");

    public static string? GetDisplayName(ClaimsPrincipal user)
    {
        var given = user.FindFirstValue("given_name");
        var family = user.FindFirstValue("family_name");
        var combined = (given + " " + family).Trim();
        return string.IsNullOrEmpty(combined)
            ? user.FindFirstValue("name")
            : combined;
    }

    public static string? GetEmail(ClaimsPrincipal user) =>
        user.FindFirstValue("email") ??
        user.FindFirstValue("preferred_username");

    // Gibt die CustomerId zurück; legt CustomerProfile beim ersten Aufruf an und hält Name/Email aktuell
    public async Task<string?> EnsureCustomerIdAsync(ClaimsPrincipal user)
    {
        var oid = GetOid(user);
        if (oid is null) return null;

        var displayName = GetDisplayName(user);
        var email = GetEmail(user);

        var profile = await db.CustomerProfiles.FirstOrDefaultAsync(cp => cp.OwnerId == oid);
        if (profile is null)
        {
            profile = new CustomerProfile
            {
                OwnerId = oid,
                CustomerId = RandomNumberGenerator.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 16),
                DisplayName = displayName,
                Email = email
            };
            db.CustomerProfiles.Add(profile);
        }
        else if (profile.DisplayName != displayName || profile.Email != email)
        {
            profile.DisplayName = displayName;
            profile.Email = email;
        }

        await db.SaveChangesAsync();
        return profile.CustomerId;
    }
}
