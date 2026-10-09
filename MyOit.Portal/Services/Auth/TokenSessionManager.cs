using System.Collections.Concurrent;
using System.Security.Cryptography;
using IotWelt.Common.Auth;
using Microsoft.AspNetCore.WebUtilities;

namespace MyOit.Portal.Services.Auth;

// Verwaltet die Portal-Sitzungen: anlegen, gültiges Access-Token liefern (mit Refresh), beenden.
// Singleton, weil die Sperren pro Sitzung für alle Circuits und Requests dieselben sein müssen.
//
// Warum die Sperre? Das Refresh-Token ist nur EINMAL verwendbar. Erneuern zwei Anfragen derselben
// Sitzung gleichzeitig, schickt die zweite ein bereits verbrauchtes Token — die API wertet das als
// Diebstahl und widerruft ALLE Sitzungen des Logins. Deshalb läuft jede Verwendung des Refresh-Tokens
// (Refresh, Logout, später Kontowechsel) pro Sitzung nacheinander.
public sealed class TokenSessionManager(
    ITokenStore store,
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<TokenSessionManager> logger)
{
    // So lange vor Ablauf wird schon erneuert — deckt Uhrabweichung und Laufzeit der Anfrage ab
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

    // Legt nach dem Login eine neue Sitzung an und liefert ihre ID für den Cookie-Claim
    public async Task<string> CreateSessionAsync(TokenResponse tokens)
    {
        var sessionId = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await store.SetAsync(sessionId, tokens);
        return sessionId;
    }

    public async Task<bool> SessionExistsAsync(string sessionId) =>
        await store.GetAsync(sessionId) is not null;

    // Gültiges Access-Token oder null, wenn die Sitzung nicht (mehr) besteht → Aufrufer muss zum Login
    public async Task<string?> GetAccessTokenAsync(string sessionId, CancellationToken ct = default)
    {
        var tokens = await store.GetAsync(sessionId);
        if (tokens is null)
            return null;
        if (!NeedsRefresh(tokens))
            return tokens.AccessToken;

        return await WithSessionLockAsync(sessionId, ct, async () =>
        {
            // Erneut lesen: Während wir auf die Sperre gewartet haben, hat evtl. eine parallele
            // Anfrage schon erneuert — dann ihr Ergebnis nehmen statt das alte Token erneut zu senden.
            tokens = await store.GetAsync(sessionId);
            if (tokens is null)
                return null;
            if (!NeedsRefresh(tokens))
                return tokens.AccessToken;

            return await RefreshAsync(sessionId, tokens.RefreshToken);
        });
    }

    // Logout: Refresh-Token bei der API widerrufen (best effort) und die Sitzung lokal vergessen
    public async Task EndSessionAsync(string sessionId)
    {
        await WithSessionLockAsync(sessionId, CancellationToken.None, async () =>
        {
            var tokens = await store.GetAsync(sessionId);
            if (tokens is not null)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AuthApiClient>().LogoutAsync(tokens.RefreshToken);
                }
                catch (Exception ex)
                {
                    // Lokal wird trotzdem abgemeldet; das Refresh-Token läuft bei der API von selbst ab
                    logger.LogWarning(ex, "Widerruf des Refresh-Tokens beim Logout fehlgeschlagen");
                }
            }
            await ForgetSessionAsync(sessionId);
            return true;
        });
    }

    private async Task<string?> RefreshAsync(string sessionId, string refreshToken)
    {
        AuthResult<TokenResponse> result;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var authApi = scope.ServiceProvider.GetRequiredService<AuthApiClient>();

            // Bewusst KEIN CancellationToken des Aufrufers: Verbraucht die API das Token und wir brechen
            // ab, bevor das neue gespeichert ist, würde der nächste Refresh das alte Token wiederverwenden.
            result = await authApi.RefreshAsync(refreshToken, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Ob die API das Token schon verbraucht hat, ist unklar. Ein erneuter Versuch damit könnte
            // alle Sitzungen widerrufen — sicherer ist, diese Sitzung zu beenden (→ neuer Login).
            logger.LogWarning(ex, "Token-Refresh fehlgeschlagen, Portal-Sitzung wird beendet");
            await ForgetSessionAsync(sessionId);
            return null;
        }

        if (!result.Succeeded)
        {
            logger.LogInformation("Token-Refresh abgelehnt ({ErrorCode}), Portal-Sitzung wird beendet", result.ErrorCode);
            await ForgetSessionAsync(sessionId);
            return null;
        }

        await store.SetAsync(sessionId, result.Value!);
        return result.Value!.AccessToken;
    }

    private bool NeedsRefresh(TokenResponse tokens) =>
        tokens.AccessTokenExpiresAt - RefreshMargin <= time.GetUtcNow().UtcDateTime;

    private async Task<T> WithSessionLockAsync<T>(string sessionId, CancellationToken ct, Func<Task<T>> action)
    {
        var gate = locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }

    // Entfernt Tokens und Sperre. Wer noch auf die alte Sperre wartet, findet danach keine Tokens
    // mehr im Store und bekommt null — eine neu angelegte Sperre schadet daher nicht.
    private async Task ForgetSessionAsync(string sessionId)
    {
        await store.RemoveAsync(sessionId);
        locks.TryRemove(sessionId, out _);
    }
}
