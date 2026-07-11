using IotWelt.Common;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyOit.Portal.Services;

public class IotWeltApiClient(
    HttpClient http,
    ITokenAcquisition tokenAcquisition,
    IConfiguration config,
    AuthenticationStateProvider authStateProvider)
{
    private async Task AuthorizeAsync()
    {
        var scope = config["IotWeltApi:Scopes"]!;
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var token = await tokenAcquisition.GetAccessTokenForUserAsync(
            [scope],
            user: authState.User);
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<List<DeviceDashboardDto>> GetDashboardAsync()
    {
        await AuthorizeAsync();
        return await http.GetFromJsonAsync<List<DeviceDashboardDto>>("/api/devices/dashboard") ?? [];
    }

    public async Task<DeviceDashboardDto?> GetDeviceAsync(int id)
    {
        await AuthorizeAsync();
        return await http.GetFromJsonAsync<DeviceDashboardDto>($"/api/devices/{id}");
    }

    public async Task<List<KlimaVerlaufPunkt>> GetKlimaVerlaufAsync(int deviceId, int minutes = 60)
    {
        await AuthorizeAsync();
        return await http.GetFromJsonAsync<List<KlimaVerlaufPunkt>>(
            $"/api/raumklimalog/{deviceId}?minutes={minutes}") ?? [];
    }

    public async Task<string?> GetCustomerIdAsync()
    {
        await AuthorizeAsync();
        var result = await http.GetFromJsonAsync<CustomerProfileResponse>("/api/customers/me");
        return result?.CustomerId;
    }

    public async Task<List<DeviceDashboardDto>> GetDevicesAsync()
    {
        await AuthorizeAsync();
        return await http.GetFromJsonAsync<List<DeviceDashboardDto>>("/api/devices") ?? [];
    }

    public async Task UpdateDeviceAsync(int id, DeviceUpdateDto dto)
    {
        await AuthorizeAsync();
        var response = await http.PutAsJsonAsync($"/api/devices/{id}", dto);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDeviceAsync(int id)
    {
        await AuthorizeAsync();
        var response = await http.DeleteAsync($"/api/devices/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResult<AdminDeviceDto>> GetAdminDevicesAsync(int skip, int take, string? customerId)
    {
        await AuthorizeAsync();
        var url = $"/api/admin/devices?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(customerId))
            url += $"&customerId={Uri.EscapeDataString(customerId)}";
        return await http.GetFromJsonAsync<PagedResult<AdminDeviceDto>>(url)
            ?? new PagedResult<AdminDeviceDto>([], 0);
    }

    public async Task AdminUpdateDeviceAsync(int id, AdminDeviceUpdateDto dto)
    {
        await AuthorizeAsync();
        var response = await http.PutAsJsonAsync($"/api/admin/devices/{id}", dto);
        response.EnsureSuccessStatusCode();
    }

    public async Task AdminDeleteDeviceAsync(int id)
    {
        await AuthorizeAsync();
        var response = await http.DeleteAsync($"/api/admin/devices/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<CustomerProfileDto>> GetAdminCustomersAsync()
    {
        await AuthorizeAsync();
        return await http.GetFromJsonAsync<List<CustomerProfileDto>>("/api/admin/customers") ?? [];
    }

    public async Task AdminDeleteCustomerAsync(string ownerId)
    {
        await AuthorizeAsync();
        var response = await http.DeleteAsync($"/api/admin/customers/{Uri.EscapeDataString(ownerId)}");
        response.EnsureSuccessStatusCode();
    }

    private record CustomerProfileResponse(string CustomerId);
}