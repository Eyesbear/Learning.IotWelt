namespace MyOit.Portal.Services.Auth;

// Claim-Typen, die das Portal selbst ins Anmelde-Cookie schreibt
public static class PortalClaims
{
    // Schlüssel der serverseitig gespeicherten Tokens (ITokenStore) — die Tokens selbst stehen nie im Cookie
    public const string SessionId = "portal_sid";
}
