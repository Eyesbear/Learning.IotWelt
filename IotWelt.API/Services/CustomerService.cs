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

    // Gibt die CustomerId zurück; legt CustomerProfile beim ersten Aufruf an
    public async Task<string?> EnsureCustomerIdAsync(ClaimsPrincipal user)
    {
        var oid = GetOid(user);
        if (oid is null) return null;

        var profile = await db.CustomerProfiles.FirstOrDefaultAsync(cp => cp.OwnerId == oid);
        if (profile is null)
        {
            profile = new CustomerProfile
            {
                OwnerId = oid,
                CustomerId = RandomNumberGenerator.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 16)
            };
            db.CustomerProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        return profile.CustomerId;
    }
}
