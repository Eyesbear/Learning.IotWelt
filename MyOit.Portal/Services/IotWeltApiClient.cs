using IotWelt.Common;
using System.Net.Http.Json;

namespace MyOit.Portal.Services;

// Das Access-Token setzt der BearerTokenHandler; der HttpClient ist keyed scoped registriert,
// damit der Handler den angemeldeten Benutzer aus dem Scope der Komponente kennt (siehe Program.cs).
public class IotWeltApiClient([FromKeyedServices(IotWeltApiClient.HttpClientName)] HttpClient http)
{
    public const string HttpClientName = "IotWeltApi";

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

    public async Task<List<CustomerProfileDto>> GetAdminCustomersAsync()
    {
        return await http.GetFromJsonAsync<List<CustomerProfileDto>>("/api/admin/customers") ?? [];
    }

    public async Task AdminDeleteCustomerAsync(string ownerId)
    {
        var response = await http.DeleteAsync($"/api/admin/customers/{Uri.EscapeDataString(ownerId)}");
        response.EnsureSuccessStatusCode();
    }

    private record CustomerProfileResponse(string CustomerId);
}