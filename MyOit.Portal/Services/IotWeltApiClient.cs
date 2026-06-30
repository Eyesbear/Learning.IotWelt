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
}