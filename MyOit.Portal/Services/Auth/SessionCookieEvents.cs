using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MyOit.Portal.Services.Auth;

// Prüft bei jedem HTTP-Request, ob die Portal-Sitzung des Cookies noch Tokens hat.
// Fehlen sie (Portal-Neustart, Logout in anderem Tab, Refresh gescheitert), wird das Cookie
// verworfen — [Authorize] schickt dann zum Login statt dass später ein API-Aufruf scheitert.
public sealed class SessionCookieEvents(TokenSessionManager sessions) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var sessionId = context.Principal?.FindFirst(PortalClaims.SessionId)?.Value;
        if (sessionId is not null && await sessions.SessionExistsAsync(sessionId))
            return;

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
