using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Authorization;

namespace MyOit.Portal.Services.Auth;

// Hängt das Access-Token der aktuellen Portal-Sitzung an Anfragen an die API und erneuert es bei Bedarf.
// Den Benutzer liest er aus dem Scope der aufrufenden Komponente (siehe ApplicationScopeHttpClientExtensions),
// nicht aus seinem eigenen DI-Scope. Nur für Clients, die aus Razor-Komponenten heraus genutzt werden:
// in Minimal-API-Endpoints ist der AuthenticationStateProvider nicht initialisiert.
public sealed class BearerTokenHandler(TokenSessionManager sessions) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sessionId = await GetSessionIdAsync(request);
        if (sessionId is not null)
        {
            var accessToken = await sessions.GetAccessTokenAsync(sessionId, cancellationToken);
            if (accessToken is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        // Ohne Token geht die Anfrage trotzdem raus: Die API antwortet mit 401, die Seite leitet zum Login
        return await base.SendAsync(request, cancellationToken);
    }

    private static async Task<string?> GetSessionIdAsync(HttpRequestMessage request)
    {
        if (!request.Options.TryGetValue(ApplicationScopeHttpClientExtensions.ScopeKey, out var services))
            throw new InvalidOperationException(
                $"{nameof(BearerTokenHandler)} braucht AddApplicationScopeHandler() vor AddHttpMessageHandler<{nameof(BearerTokenHandler)}>().");

        var authState = await services.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
        return authState.User.FindFirst(PortalClaims.SessionId)?.Value;
    }
}
