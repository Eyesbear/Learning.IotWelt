using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.Common.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IotWelt.API.Services;

// Stellt Access-Tokens (JWT, HS256) und Refresh-Tokens aus, rotiert und widerruft sie.
public class TokenService(
    AppDbContext db,
    UserManager<AppUser> users,
    AccountService accounts,
    IOptions<JwtOptions> options,
    TimeProvider time,
    ILogger<TokenService> logger)
{
    private readonly JwtOptions _jwt = options.Value;

    // Neues Token-Paar für den Login im Kontext des gegebenen Kontos (null = kein Konto)
    public async Task<TokenResponse> IssueAsync(AppUser user, AccountMembership? membership)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var accessExpires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(CurrentAccount.UserIdClaim, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("name", user.DisplayName ?? user.Email ?? user.Id)
        };
        claims.AddRange((await users.GetRolesAsync(user)).Select(r => new Claim("role", r)));

        if (membership is not null)
        {
            claims.Add(new(CurrentAccount.AccountIdClaim, membership.Account.CustomerId));
            claims.Add(new(CurrentAccount.AccountRoleClaim, membership.Role.ToString()));
        }

        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = accessExpires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_jwt.GetKeyBytes()), SecurityAlgorithms.HmacSha256)
        });

        var refreshToken = NewRefreshToken();
        var refreshExpires = now.AddDays(_jwt.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refreshToken),
            AccountId = membership?.AccountId,
            CreatedAt = now,
            ExpiresAt = refreshExpires
        });

        if (membership is not null && user.LastActiveAccountId != membership.AccountId)
            user.LastActiveAccountId = membership.AccountId;   // wird mit SaveChanges gespeichert

        await db.SaveChangesAsync();
        return new TokenResponse(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    // Rotation: altes Token wird verbraucht, neues ausgestellt. Mitgliedschaft und Rolle werden dabei
    // frisch aus der DB gelesen — entzogene Zugriffe wirken spätestens beim nächsten Refresh (Story C3).
    // preferredAccountId != null → Kontowechsel (Story D2).
    public async Task<TokenResponse?> RefreshAsync(string refreshToken, int? preferredAccountId = null)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is null)
            return null;

        if (stored.RevokedAt is not null)
        {
            // Ein bereits ersetztes Token wird erneut verwendet → vermutlich gestohlen.
            // Vorsichtshalber alle Sitzungen dieses Logins beenden.
            if (stored.ReplacedByHash is not null)
            {
                logger.LogWarning("Wiederverwendung eines Refresh-Tokens für Login {UserId} — alle Sitzungen werden widerrufen", stored.UserId);
                await RevokeAllAsync(stored.UserId);
            }
            return null;
        }

        if (stored.ExpiresAt <= now || await users.IsLockedOutAsync(stored.User))
            return null;

        var membership = await accounts.ResolveActiveAsync(stored.User, preferredAccountId ?? stored.AccountId);
        if (preferredAccountId is not null && membership?.AccountId != preferredAccountId)
            return null;   // Kontowechsel in ein Konto ohne Mitgliedschaft

        // Altes Token verbrauchen — wird zusammen mit dem neuen Token in IssueAsync gespeichert
        stored.RevokedAt = now;
        var issued = await IssueAsync(stored.User, membership);
        stored.ReplacedByHash = Hash(issued.RefreshToken);
        await db.SaveChangesAsync();
        return issued;
    }

    public async Task RevokeAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
        }
    }

    // Alle aktiven Refresh-Tokens eines Logins widerrufen (Passwortänderung, Sperre, Token-Diebstahl)
    public async Task RevokeAllAsync(string userId)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
    }

    // 64 zufällige Bytes, URL-sicher kodiert — das Token selbst verlässt die API nur einmal
    private static string NewRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
