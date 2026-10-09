using IotWelt.Common.Auth;

namespace MyOit.Portal.Services.Auth;

// Serverseitige Ablage der API-Tokens je Portal-Sitzung (Schlüssel: Claim PortalClaims.SessionId).
// Asynchron, damit später ein verteilter Speicher (Redis, DB) ohne Umbau der Aufrufer passt.
public interface ITokenStore
{
    Task<TokenResponse?> GetAsync(string sessionId);
    Task SetAsync(string sessionId, TokenResponse tokens);
    Task RemoveAsync(string sessionId);
}
