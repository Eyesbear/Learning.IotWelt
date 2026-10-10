namespace IotWelt.Common.Auth;

// Verträge der Auth-Endpoints (/api/auth/...) — gemeinsam genutzt von API, Portal und MAUI-Client.

public record RegisterRequest(string Email, string Password, string? DisplayName);

public record ConfirmEmailRequest(string UserId, string Code);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record SwitchAccountRequest(string CustomerId, string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Code, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// A4: eigenen Login löschen — Passwort als Bestätigung, damit ein abgegriffenes Access-Token nicht reicht
public record DeleteLoginRequest(string Password);

// Access-Token (kurzlebig, JWT) + Refresh-Token (langlebig, opak, nur einmal verwendbar)
public record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

public record MeResponse(
    string UserId,
    string Email,
    string? DisplayName,
    bool IsAdmin,
    string? CustomerId,
    string? AccountRole);

// Ein Konto, dem der angemeldete Login angehört (für den Kontowechsler)
public record AccountSummaryDto(string CustomerId, string Name, string Role, bool IsActive);

// Fehlercodes im "title" der ProblemDetails bei 401 auf /login — Clients zeigen passende Texte an
public static class AuthErrors
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string LockedOut = "locked_out";
    public const string EmailNotConfirmed = "email_not_confirmed";
    public const string InvalidRefreshToken = "invalid_refresh_token";
}

// Fehlercodes im "title" der ProblemDetails bei 409 auf DELETE /me
public static class DeleteLoginErrors
{
    // Login ist Owner mindestens eines Kontos — erst Konto löschen oder Ownership übertragen
    public const string OwnsAccounts = "owns_accounts";
}
