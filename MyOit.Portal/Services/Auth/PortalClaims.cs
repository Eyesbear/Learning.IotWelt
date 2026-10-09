using System.Security.Claims;
using IotWelt.Common.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MyOit.Portal.Services.Auth;

// Claim-Typen, die das Portal selbst ins Anmelde-Cookie schreibt
public static class PortalClaims
{
    // Schlüssel der serverseitig gespeicherten Tokens (ITokenStore) — die Tokens selbst stehen nie im Cookie
    public const string SessionId = "portal_sid";

    // Aktives Konto und Rolle darin — gleiche Namen wie in den Tokens der API
    public const string AccountId = "account_id";
    public const string AccountRole = "account_role";

    // Baut den Cookie-Benutzer aus /api/auth/me. Die Claims dienen nur der Anzeige und der
    // Navigation im Portal; über die Daten entscheidet allein die API anhand des Access-Tokens.
    public static ClaimsPrincipal CreatePrincipal(MeResponse me, string sessionId)
    {
        List<Claim> claims =
        [
            new(SessionId, sessionId),
            new(ClaimTypes.NameIdentifier, me.UserId),
            new(ClaimTypes.Name, me.DisplayName ?? me.Email),
            new(ClaimTypes.Email, me.Email),
        ];
        if (me.IsAdmin)
            claims.Add(new(ClaimTypes.Role, "Admin"));
        if (me.CustomerId is not null)
            claims.Add(new(AccountId, me.CustomerId));
        if (me.AccountRole is not null)
            claims.Add(new(AccountRole, me.AccountRole));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
