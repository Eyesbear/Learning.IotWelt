using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common;

namespace IotWelt.API.Tests;

// Kundenprofil (Auto-Anlage beim ersten Aufruf) und Admin-Endpoints (Rolle "Admin").
[Collection(ApiCollection.Name)]
public class CustomersAndAdminTests(ApiFactory factory)
{
    private record MeResponse(string CustomerId);

    [Fact]
    public async Task Erster_Aufruf_legt_Kundenprofil_mit_stabiler_16_stelliger_CustomerId_an()
    {
        var client = factory.AsUser(TestData.UserId());

        var erster = await client.GetFromJsonAsync<MeResponse>("/api/customers/me");
        var zweiter = await client.GetFromJsonAsync<MeResponse>("/api/customers/me");

        Assert.Matches("^[A-Z0-9]{16}$", erster!.CustomerId);
        Assert.Equal(erster.CustomerId, zweiter!.CustomerId);
    }

    [Fact]
    public async Task Admin_Endpoints_ohne_Admin_Rolle_geben_403()
    {
        var client = factory.AsUser(TestData.UserId());

        var response = await client.GetAsync("/api/admin/devices");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_sieht_Geraete_fremder_Kunden_mit_Besitzerdaten()
    {
        var userId = TestData.UserId();
        var kunde = factory.AsUser(userId);
        var customerId = (await kunde.GetFromJsonAsync<MeResponse>("/api/customers/me"))!.CustomerId;
        await kunde.PostAsJsonAsync("/api/devices", new { name = "Kundengeraet" });

        var admin = factory.AsUser(TestData.UserId(), "Admin");
        var page = await admin.GetFromJsonAsync<PagedResult<AdminDeviceDto>>(
            $"/api/admin/devices?customerId={customerId}");

        Assert.Equal(1, page!.TotalCount);
        var device = Assert.Single(page.Items);
        Assert.Equal("Kundengeraet", device.Name);
        Assert.Equal($"{userId}@test.local", device.OwnerEmail);
    }

    [Fact]
    public async Task Admin_loescht_Kunden_samt_Geraeten()
    {
        var userId = TestData.UserId();
        var kunde = factory.AsUser(userId);
        var customerId = (await kunde.GetFromJsonAsync<MeResponse>("/api/customers/me"))!.CustomerId;
        await kunde.PostAsJsonAsync("/api/devices", new { name = "Weg" });
        var admin = factory.AsUser(TestData.UserId(), "Admin");

        var delete = await admin.DeleteAsync($"/api/admin/customers/{userId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var page = await admin.GetFromJsonAsync<PagedResult<AdminDeviceDto>>(
            $"/api/admin/devices?customerId={customerId}");
        Assert.Equal(0, page!.TotalCount);
    }
}
