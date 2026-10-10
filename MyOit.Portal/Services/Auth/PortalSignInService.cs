using IotWelt.Common.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MyOit.Portal.Services.Auth;

// Macht aus einem Token-Paar der API eine Portal-Anmeldung: Sitzung anlegen, Benutzer laden, Cookie ausstellen.
// Nur in einem echten HTTP-Request aufrufbar (SSR-Seite oder Endpoint) — im Circuit lässt sich kein Cookie setzen.
public sealed class PortalSignInService(TokenSessionManager sessions, AuthApiClient authApi)
{
    public async Task SignInAsync(HttpContext httpContext, TokenResponse tokens, bool isPersistent)
    {
        var sessionId = await sessions.CreateSessionAsync(tokens);
        try
        {
            await IssueCookieAsync(httpContext, sessionId, tokens, isPersistent);
        }
        catch
        {
            await sessions.EndSessionAsync(sessionId);
            throw;
        }
    }

    // Nach dem Kontowechsel: gleiche Sitzung, aber neues Cookie mit den Claims des neuen Kontos.
    // "Angemeldet bleiben" wird vom bisherigen Cookie übernommen.
    public async Task RenewCookieAsync(HttpContext httpContext, string sessionId, TokenResponse tokens)
    {
        var current = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        try
        {
            await IssueCookieAsync(httpContext, sessionId, tokens, current.Properties?.IsPersistent ?? false);
        }
        catch
        {
            await sessions.EndSessionAsync(sessionId);
            throw;
        }
    }

    private async Task IssueCookieAsync(HttpContext httpContext, string sessionId, TokenResponse tokens, bool isPersistent)
    {
        var me = await authApi.GetMeAsync(tokens.AccessToken, httpContext.RequestAborted);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            PortalClaims.CreatePrincipal(me, sessionId),
            new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                // Nie länger als das Refresh-Token — danach gäbe es zum Cookie keine gültige Sitzung mehr
                ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(tokens.RefreshTokenExpiresAt, DateTimeKind.Utc)),
            });
    }
}
