namespace IotWelt.API.Services;

// Section "Jwt" — SigningKey kommt NIE aus appsettings, sondern aus User-Secrets / Env-Var (Jwt__SigningKey)
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "iotwelt-api";
    public string Audience { get; set; } = "iotwelt-clients";

    // Base64, mindestens 32 Bytes (256 Bit) für HS256
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;

    public byte[] GetKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
            throw new InvalidOperationException(
                "Jwt:SigningKey fehlt. Lokal per 'dotnet user-secrets set \"Jwt:SigningKey\" <base64>' setzen (siehe CLAUDE.md).");

        var bytes = Convert.FromBase64String(SigningKey);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey ist zu kurz — HS256 braucht mindestens 32 Bytes.");
        return bytes;
    }
}

// Section "App" — Basis-URL des Portals für Links in E-Mails (Bestätigung, Passwort-Reset)
public class AppLinkOptions
{
    public const string SectionName = "App";

    public string PortalBaseUrl { get; set; } = "https://localhost:7008";
}
