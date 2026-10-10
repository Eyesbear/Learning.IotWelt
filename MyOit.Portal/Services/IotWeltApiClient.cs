using IotWelt.Common;
using IotWelt.Common.Admin;
using IotWelt.Common.Auth;
using IotWelt.Common.Members;
using MyOit.Portal.Services.Auth;
using System.Net;
using System.Net.Http.Json;

namespace MyOit.Portal.Services;

// Das Access-Token setzt der BearerTokenHandler; der HttpClient ist keyed scoped registriert,
// damit der Handler den angemeldeten Benutzer aus dem Scope der Komponente kennt (siehe Program.cs).
public class IotWeltApiClient([FromKeyedServices(IotWeltApiClient.HttpClientName)] HttpClient http)
{
    public const string HttpClientName = "IotWeltApi";

    // Alle Konten des Logins für den Kontowechsler (IsActive = das Konto der aktuellen Sitzung)
    public async Task<List<AccountSummaryDto>> GetMyAccountsAsync()
    {
        return await http.GetFromJsonAsync<List<AccountSummaryDto>>("/api/auth/accounts") ?? [];
    }

    // C1: Mitglieder und offene Einladungen des aktiven Kontos (nur Owner, sonst 403)
    public async Task<MembersOverviewDto> GetMembersAsync()
    {
        return (await http.GetFromJsonAsync<MembersOverviewDto>("/api/members"))!;
    }

    // B1: 400 = E-Mail/Rolle ungültig, 409 MemberErrors.AlreadyMember. Erneutes Einladen derselben
    // Adresse ersetzt die offene Einladung (neuer Link, alter wird ungültig).
    public async Task<AuthResult> InviteMemberAsync(string email, string role)
    {
        var response = await http.PostAsJsonAsync("/api/members/invitations", new InviteMemberRequest(email, role));
        return await ToResultAsync(response, default);
    }

    public async Task<AuthResult> RevokeInvitationAsync(int invitationId)
    {
        var response = await http.DeleteAsync($"/api/members/invitations/{invitationId}");
        return await ToResultAsync(response, default);
    }

    // C2: nur Editor ↔ Reader; wirkt beim nächsten Token-Refresh des Mitglieds
    public async Task<AuthResult> ChangeMemberRoleAsync(string userId, string role)
    {
        var response = await http.PutAsJsonAsync($"/api/members/{Uri.EscapeDataString(userId)}/role", new ChangeMemberRoleRequest(role));
        return await ToResultAsync(response, default);
    }

    // C3: Zugriff entziehen; wirkt beim nächsten Token-Refresh des Mitglieds
    public async Task<AuthResult> RemoveMemberAsync(string userId)
    {
        var response = await http.DeleteAsync($"/api/members/{Uri.EscapeDataString(userId)}");
        return await ToResultAsync(response, default);
    }

    // B3: Einladung mit dem angemeldeten Login annehmen. Liefert das neue Konto (noch nicht aktiv —
    // der Aufrufer wechselt per Kontowechsel hinein). 403 = andere E-Mail, 409 schon Mitglied, 410 abgelaufen/verbraucht.
    public async Task<AuthResult<AccountSummaryDto>> AcceptInvitationAsync(string token, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/invitations/{Uri.EscapeDataString(token)}/accept", content: null, ct);
        if (response.IsSuccessStatusCode)
            return new(await response.Content.ReadFromJsonAsync<AccountSummaryDto>(ct), true);

        var failure = await ToResultAsync(response, ct);
        return new(default, false, failure.ErrorCode, failure.Errors);
    }

    // C4: aktives Konto samt Geräten und Messwerten löschen. Fehlercode MemberErrors.NotOwner (409) oder
    // AuthApiClient.ForbiddenErrorCode (403), wenn der Login nicht (mehr) Owner ist.
    // Das Access-Token nennt danach noch das gelöschte Konto — die Sitzung muss erneuert werden.
    public async Task<AuthResult> DeleteActiveAccountAsync(CancellationToken ct = default)
    {
        var response = await http.DeleteAsync("/api/members/account", ct);
        return await ToResultAsync(response, ct);
    }

    // A4: eigenen Login löschen. 400 = Passwort falsch, 409 DeleteLoginErrors.OwnsAccounts = noch Owner eines Kontos
    public async Task<AuthResult> DeleteMyLoginAsync(string password, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/me")
        {
            Content = JsonContent.Create(new DeleteLoginRequest(password)),
        };
        var response = await http.SendAsync(request, ct);
        return await ToResultAsync(response, ct);
    }

    public async Task<List<DeviceDashboardDto>> GetDashboardAsync()
    {
        return await http.GetFromJsonAsync<List<DeviceDashboardDto>>("/api/devices/dashboard") ?? [];
    }

    public async Task<DeviceDashboardDto?> GetDeviceAsync(int id)
    {
        return await http.GetFromJsonAsync<DeviceDashboardDto>($"/api/devices/{id}");
    }

    public async Task<List<KlimaVerlaufPunkt>> GetKlimaVerlaufAsync(int deviceId, int minutes = 60, string? range = null)
    {
        var url = range is not null
            ? $"/api/raumklimalog/{deviceId}?range={Uri.EscapeDataString(range)}"
            : $"/api/raumklimalog/{deviceId}?minutes={minutes}";
        return await http.GetFromJsonAsync<List<KlimaVerlaufPunkt>>(url) ?? [];
    }

    public async Task<string?> GetCustomerIdAsync()
    {
        var result = await http.GetFromJsonAsync<CustomerProfileResponse>("/api/customers/me");
        return result?.CustomerId;
    }

    public async Task<List<DeviceDashboardDto>> GetDevicesAsync()
    {
        return await http.GetFromJsonAsync<List<DeviceDashboardDto>>("/api/devices") ?? [];
    }

    public async Task UpdateDeviceAsync(int id, DeviceUpdateDto dto)
    {
        var response = await http.PutAsJsonAsync($"/api/devices/{id}", dto);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDeviceAsync(int id)
    {
        var response = await http.DeleteAsync($"/api/devices/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResult<AdminDeviceDto>> GetAdminDevicesAsync(int skip, int take, string? customerId)
    {
        var url = $"/api/admin/devices?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(customerId))
            url += $"&customerId={Uri.EscapeDataString(customerId)}";
        return await http.GetFromJsonAsync<PagedResult<AdminDeviceDto>>(url)
            ?? new PagedResult<AdminDeviceDto>([], 0);
    }

    public async Task AdminUpdateDeviceAsync(int id, AdminDeviceUpdateDto dto)
    {
        var response = await http.PutAsJsonAsync($"/api/admin/devices/{id}", dto);
        response.EnsureSuccessStatusCode();
    }

    public async Task AdminDeleteDeviceAsync(int id)
    {
        var response = await http.DeleteAsync($"/api/admin/devices/{id}");
        response.EnsureSuccessStatusCode();
    }

    // E1: alle Logins mit Konten und Rollen
    public async Task<List<AdminLoginDto>> GetAdminLoginsAsync()
    {
        return await http.GetFromJsonAsync<List<AdminLoginDto>>("/api/admin/logins") ?? [];
    }

    // E2: sperren widerruft auch alle Refresh-Tokens; 409 AdminLoginErrors.SelfAction am eigenen Login
    public async Task<AuthResult> AdminSetLoginLockedAsync(string userId, bool locked)
    {
        var action = locked ? "lock" : "unlock";
        var response = await http.PostAsync($"/api/admin/logins/{Uri.EscapeDataString(userId)}/{action}", content: null);
        return await ToResultAsync(response, default);
    }

    // E3: wirkt beim nächsten Token-Refresh des Logins; entziehen am eigenen Login → 409 SelfAction
    public async Task<AuthResult> AdminSetLoginAdminAsync(string userId, bool isAdmin)
    {
        var url = $"/api/admin/logins/{Uri.EscapeDataString(userId)}/admin";
        var response = isAdmin ? await http.PutAsync(url, content: null) : await http.DeleteAsync(url);
        return await ToResultAsync(response, default);
    }

    // 409 DeleteLoginErrors.OwnsAccounts, solange der Login Owner ist (erst AdminDeleteOwnedAccountsAsync)
    public async Task<AuthResult> AdminDeleteLoginAsync(string userId)
    {
        var response = await http.DeleteAsync($"/api/admin/logins/{Uri.EscapeDataString(userId)}");
        return await ToResultAsync(response, default);
    }

    // Löscht ALLE Konten, deren Owner der Login ist, samt Geräten und Messwerten — der Login bleibt
    public async Task<AuthResult> AdminDeleteOwnedAccountsAsync(string ownerId)
    {
        var response = await http.DeleteAsync($"/api/admin/logins/{Uri.EscapeDataString(ownerId)}/accounts");
        return await ToResultAsync(response, default);
    }

    // Vom Portal vergeben: Mitglied/Einladung gibt es nicht (mehr) — z. B. in einem anderen Tab schon entfernt
    public const string NotFoundErrorCode = "not_found";

    // 401 als Exception wie bei den übrigen Aufrufen — die Seiten leiten dann zum Login
    private static async Task<AuthResult> ToResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return new AuthResult(true);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new AuthResult(false, NotFoundErrorCode);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            response.EnsureSuccessStatusCode();
        return await AuthApiClient.ReadFailureAsync(response, ct);
    }

    private record CustomerProfileResponse(string CustomerId);
}