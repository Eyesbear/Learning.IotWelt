using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IotWelt.Common.Auth;
using IotWelt.Common.Members;
using Microsoft.AspNetCore.Http;

namespace MyOit.Portal.Services.Auth;

// Ergebnis eines Auth-Aufrufs: Fehlercode aus AuthErrors (Login/Refresh) bzw. Meldungen der API (Validierung)
public record AuthResult(bool Succeeded, string? ErrorCode = null, IReadOnlyList<string>? Errors = null);

public sealed record AuthResult<T>(T? Value, bool Succeeded, string? ErrorCode = null, IReadOnlyList<string>? Errors = null)
    : AuthResult(Succeeded, ErrorCode, Errors);

// Client für /api/auth/*. Läuft ohne BearerTokenHandler: Token werden — wo nötig — explizit übergeben,
// weil diese Aufrufe auch außerhalb von Komponenten (Login-Seite, Refresh, Logout) stattfinden.
public sealed class AuthApiClient(HttpClient http)
{
    // Kein Code der API (die antwortet bei 403 ohne Body), sondern vom Portal vergeben
    public const string ForbiddenErrorCode = "forbidden";

    // Legt Login + eigenes Konto an; die API verschickt danach die Bestätigungsmail
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/auth/register", request, ct);
        return response.IsSuccessStatusCode ? new AuthResult(true) : await ReadFailureAsync(response, ct);
    }

    public async Task<AuthResult> ConfirmEmailAsync(string userId, string code, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(userId, code), ct);
        return response.IsSuccessStatusCode ? new AuthResult(true) : await ReadFailureAsync(response, ct);
    }

    // Die API antwortet immer mit 200 — auch für unbekannte Adressen (kein Rückschluss auf registrierte Konten)
    public async Task<AuthResult> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), ct);
        return response.IsSuccessStatusCode ? new AuthResult(true) : await ReadFailureAsync(response, ct);
    }

    // Bei Erfolg widerruft die API alle Refresh-Tokens des Logins — auch die der Portal-Sitzungen
    public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/auth/reset-password", request, ct);
        return response.IsSuccessStatusCode ? new AuthResult(true) : await ReadFailureAsync(response, ct);
    }

    public async Task<AuthResult<TokenResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), ct);
        return await ReadTokenResultAsync(response, ct);
    }

    public async Task<AuthResult<TokenResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken), ct);
        return await ReadTokenResultAsync(response, ct);
    }

    // Widerruft das Refresh-Token; die API antwortet immer mit 204
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(refreshToken), ct);
        response.EnsureSuccessStatusCode();
    }

    // Verbraucht das Refresh-Token — nur unter der Sitzungssperre aufrufen (TokenSessionManager).
    // 403 (fremdes Konto) kommt, bevor die API das Token anfasst; es bleibt dann gültig.
    public async Task<AuthResult<TokenResponse>> SwitchAccountAsync(
        string accessToken, string customerId, string refreshToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/switch-account")
        {
            Content = JsonContent.Create(new SwitchAccountRequest(customerId, refreshToken)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.SendAsync(request, ct);
        return await ReadTokenResultAsync(response, ct);
    }

    // Widerruft bei Erfolg alle Refresh-Tokens des Logins und liefert ein neues Paar für diese Sitzung —
    // nur unter der Sitzungssperre aufrufen (TokenSessionManager). 400 = bisheriges Passwort falsch oder Regeln verletzt.
    public async Task<AuthResult<TokenResponse>> ChangePasswordAsync(
        string accessToken, ChangePasswordRequest changeRequest, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(changeRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.SendAsync(request, ct);
        return await ReadTokenResultAsync(response, ct);
    }

    // C5 (eigentlich /api/members): hier, weil der Formular-Endpoint /account/transfer-ownership außerhalb
    // einer Komponente läuft und danach ohnehin Tokens und Cookie erneuert. 400 = Ziel ist schon Owner,
    // 409 MemberErrors.NotOwner, 403 = Token sagt nicht Owner. 404 (Mitglied weg) wird zur Exception.
    public async Task<AuthResult> TransferOwnershipAsync(string accessToken, string userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/members/transfer-ownership")
        {
            Content = JsonContent.Create(new TransferOwnershipRequest(userId)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? new AuthResult(true) : await ReadFailureAsync(response, ct);
    }

    public async Task<MeResponse> GetMeAsync(string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MeResponse>(ct))!;
    }

    private static async Task<AuthResult<TokenResponse>> ReadTokenResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return new(await response.Content.ReadFromJsonAsync<TokenResponse>(ct), true);

        var failure = await ReadFailureAsync(response, ct);
        return new(default, false, failure.ErrorCode, failure.Errors);
    }

    // 400 (ValidationProblem) und 401/403/409 (ProblemDetails mit Fehlercode im title) sind fachliche Fehler,
    // alles andere (500, API nicht erreichbar) ist eine Störung und wird als Exception weitergereicht.
    // Auch von IotWeltApiClient genutzt (Löschen von Konto und Login).
    internal static async Task<AuthResult> ReadFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden or HttpStatusCode.Conflict))
            response.EnsureSuccessStatusCode();

        HttpValidationProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(ct);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Leerer oder kein JSON-Body (z. B. Forbid()) — dann gibt es nur den Statuscode
        }

        // Nur bei 401/409 steht ein Fehlercode im title; bei 400 ist er der allgemeine Validierungstext.
        // 403 (Forbid()) hat keinen Body — dafür ein eigener Code des Portals.
        var errorCode = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Conflict => problem?.Title,
            HttpStatusCode.Forbidden => ForbiddenErrorCode,
            _ => null,
        };
        // Schlüssel sind bei Identity-Fehlern deren Code (z. B. PasswordTooShort) → deutscher Text, sonst die API-Meldung.
        // Distinct: Bei E-Mail = Benutzername meldet Identity DuplicateEmail und DuplicateUserName.
        var errors = problem?.Errors
            .SelectMany(error => IdentityErrorTexts.TryTranslate(error.Key, out var text) ? [text] : error.Value)
            .Distinct()
            .ToList();
        return new AuthResult(false, errorCode, errors);
    }
}
