using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common;

namespace IotWelt.API.Tests;

// Kundenkennung des aktiven Kontos und Admin-Endpoints (System-Rolle "Admin").
[Collection(ApiCollection.Name)]
public class CustomersAndAdminTests(ApiFactory factory)
{
    private record CustomerMe(string CustomerId);

    [Fact]
    public async Task Registrierung_legt_Konto_mit_16_stelliger_CustomerId_an()
    {
        var user = await factory.CreateUserAsync();

        var me = await user.Client.GetFromJsonAsync<CustomerMe>("/api/customers/me");

        Assert.Matches("^[A-Z0-9]{16}$", me!.CustomerId);
        Assert.Equal(user.CustomerId, me.CustomerId);
    }

    [Fact]
    public async Task Admin_Endpoints_ohne_Admin_Rolle_geben_403()
    {
        var user = await factory.CreateUserAsync();

        var response = await user.Client.GetAsync("/api/admin/devices");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_sieht_Geraete_fremder_Kunden_mit_Besitzerdaten()
    {
        var kunde = await factory.CreateUserAsync();
        await kunde.Client.PostAsJsonAsync("/api/devices", new { name = "Kundengeraet" });

        var admin = await factory.CreateUserAsync(admin: true);
        var page = await admin.Client.GetFromJsonAsync<PagedResult<AdminDeviceDto>>(
            $"/api/admin/devices?customerId={kunde.CustomerId}");

        Assert.Equal(1, page!.TotalCount);
        var device = Assert.Single(page.Items);
        Assert.Equal("Kundengeraet", device.Name);
        Assert.Equal(kunde.Email, device.OwnerEmail);
    }

    [Fact]
    public async Task Admin_loescht_Konten_eines_Logins_samt_Geraeten()
    {
        var kunde = await factory.CreateUserAsync();
        await kunde.Client.PostAsJsonAsync("/api/devices", new { name = "Weg" });
        var admin = await factory.CreateUserAsync(admin: true);

        var delete = await admin.Client.DeleteAsync($"/api/admin/logins/{kunde.UserId}/accounts");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var page = await admin.Client.GetFromJsonAsync<PagedResult<AdminDeviceDto>>(
            $"/api/admin/devices?customerId={kunde.CustomerId}");
        Assert.Equal(0, page!.TotalCount);
    }
}
