using IotWelt.Common.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace MyOit.Portal.Services.Auth;

// Vorerst im Arbeitsspeicher: nach einem Neustart des Portals ist der Store leer,
// die Cookie-Prüfung schickt die Benutzer dann zum Login. Nur für eine einzelne Portal-Instanz geeignet.
public sealed class InMemoryTokenStore(IMemoryCache cache) : ITokenStore
{
    private static string Key(string sessionId) => $"tokens:{sessionId}";

    public Task<TokenResponse?> GetAsync(string sessionId) =>
        Task.FromResult(cache.TryGetValue(Key(sessionId), out TokenResponse? tokens) ? tokens : null);

    public Task SetAsync(string sessionId, TokenResponse tokens)
    {
        // Der Eintrag verschwindet von selbst mit dem Refresh-Token — danach ist die Sitzung ohnehin nicht mehr zu retten
        var expires = new DateTimeOffset(DateTime.SpecifyKind(tokens.RefreshTokenExpiresAt, DateTimeKind.Utc));
        cache.Set(Key(sessionId), tokens, expires);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string sessionId)
    {
        cache.Remove(Key(sessionId));
        return Task.CompletedTask;
    }
}
