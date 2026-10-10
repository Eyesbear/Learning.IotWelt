using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace MyOit.Portal.Services.Auth;

// Formular-Endpoints, die das Cookie ändern. Sie laufen als echter HTTP-POST (mit Antiforgery-Token
// aus <AntiforgeryToken />), weil sich Cookies im interaktiven Circuit nicht setzen lassen.
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Schutz gegen CSRF (fremde Seite schickt per Auto-Submit ein Formular hierher):
        // UseAntiforgery() PRÜFT das Token nur und legt das Ergebnis in IAntiforgeryValidationFeature ab —
        // abweisen muss der Endpoint selbst. Bei gebundenen Formularfeldern übernimmt das die Bindung,
        // diese Endpoints binden aber nicht zwingend welche. Daher Prüfung verlangen + Filter für die ganze Gruppe.
        var account = endpoints.MapGroup("/account")
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .AddEndpointFilter(RejectInvalidAntiforgeryAsync);

        account.MapPost("/logout", async (HttpContext httpContext, ClaimsPrincipal user, TokenSessionManager sessions) =>
        {
            var sessionId = user.FindFirst(PortalClaims.SessionId)?.Value;
            if (sessionId is not null)
                await sessions.EndSessionAsync(sessionId);

            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.LocalRedirect("~/");
        });

        account.MapPost("/switch-account", async (
            [FromForm] string customerId,
            HttpContext httpContext, ClaimsPrincipal user,
            TokenSessionManager sessions, PortalSignInService signIn) =>
        {
            var sessionId = user.FindFirst(PortalClaims.SessionId)?.Value;
            if (sessionId is null)
                return TypedResults.LocalRedirect("~/account/login");

            var (status, tokens) = await sessions.SwitchAccountAsync(sessionId, customerId);
            switch (status)
            {
                case AccountSwitchStatus.Switched:
                    await signIn.RenewCookieAsync(httpContext, sessionId, tokens!);
                    // Zum Dashboard, nicht zurück: Die bisherige Seite (z. B. ein Gerät) gehört zum alten Konto
                    return TypedResults.LocalRedirect("~/dashboard");

                case AccountSwitchStatus.Forbidden:
                    // Nur per manipuliertem Formular erreichbar — der Wechsler bietet nur eigene Konten an
                    return TypedResults.LocalRedirect("~/");

                default:
                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return TypedResults.LocalRedirect("~/account/login");
            }
        });

        // C5 als Formular-POST statt im Circuit: Nach der Übertragung ist der Login nur noch Editor, Token und
        // Cookie sagen aber noch "Owner" — beides wird hier sofort erneuert (Cookie geht nur im HTTP-Request).
        account.MapPost("/transfer-ownership", async (
            [FromForm] string userId,
            HttpContext httpContext, ClaimsPrincipal user,
            TokenSessionManager sessions, AuthApiClient authApi, PortalSignInService signIn,
            ILoggerFactory loggers) =>
        {
            var sessionId = user.FindFirst(PortalClaims.SessionId)?.Value;
            var accessToken = sessionId is null ? null : await sessions.GetAccessTokenAsync(sessionId);
            if (accessToken is null)
                return TypedResults.LocalRedirect("~/account/login");

            AuthResult result;
            try
            {
                result = await authApi.TransferOwnershipAsync(accessToken, userId, httpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Auch 404: Das Mitglied wurde inzwischen entfernt
                loggers.CreateLogger(typeof(AccountEndpoints)).LogWarning(ex, "Übertragung der Eigentümerschaft fehlgeschlagen");
                return TypedResults.LocalRedirect("~/account/members?transfer=failed");
            }

            if (!result.Succeeded)
                return TypedResults.LocalRedirect("~/account/members?transfer=failed");

            var tokens = await sessions.RenewTokensAsync(sessionId!);
            if (tokens is null)
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return TypedResults.LocalRedirect("~/account/login");
            }

            await signIn.RenewCookieAsync(httpContext, sessionId!, tokens);
            return TypedResults.LocalRedirect("~/account/members?transfer=done");
        });

        return endpoints;
    }

    private static async ValueTask<object?> RejectInvalidAntiforgeryAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Fehlt das Feature, ist die Middleware nicht gelaufen — ebenfalls ablehnen statt durchzuwinken
        var antiforgery = context.HttpContext.Features.Get<IAntiforgeryValidationFeature>();
        // Mit Body: Eine leere 400 würde UseStatusCodePagesWithReExecute als POST auf /not-found
        // weiterreichen — die Seite liest dort das Formular und scheitert erneut am ungültigen Token (500).
        if (antiforgery is not { IsValid: true })
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ungültiges oder fehlendes Antiforgery-Token");

        return await next(context);
    }
}
