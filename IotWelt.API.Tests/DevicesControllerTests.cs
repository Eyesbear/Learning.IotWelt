using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Models;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common;

namespace IotWelt.API.Tests;

// Mandantentrennung und CRUD von /api/devices — muss den Auth-Umbau in Phase 1 unverändert überstehen.
[Collection(ApiCollection.Name)]
public class DevicesControllerTests(ApiFactory factory)
{
    private static async Task<string> GetCustomerIdAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<MeResponse>("/api/customers/me");
        return me!.CustomerId;
    }

    private static async Task<Device> CreateDeviceAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/devices", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Device>())!;
    }

    [Fact]
    public async Task Ohne_Anmeldung_gibt_es_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Neues_Geraet_gehoert_immer_dem_Aufrufer_auch_wenn_Body_anderen_Kunden_nennt()
    {
        var client = factory.AsUser(TestData.UserId());
        var eigeneId = await GetCustomerIdAsync(client);

        var response = await client.PostAsJsonAsync("/api/devices",
            new { name = "Wohnzimmer", customerId = TestData.CustomerId() });
        var device = await response.Content.ReadFromJsonAsync<Device>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(eigeneId, device!.CustomerId);
    }

    [Fact]
    public async Task Kunde_sieht_nur_eigene_Geraete()
    {
        var alice = factory.AsUser(TestData.UserId());
        var bob = factory.AsUser(TestData.UserId());
        var aliceDevice = await CreateDeviceAsync(alice, "Alice-Bad");
        var bobDevice = await CreateDeviceAsync(bob, "Bob-Kueche");

        var aliceList = await alice.GetFromJsonAsync<List<Device>>("/api/devices");

        var einziges = Assert.Single(aliceList!);
        Assert.Equal(aliceDevice.Id, einziges.Id);
        Assert.DoesNotContain(aliceList!, d => d.Id == bobDevice.Id);
    }

    [Fact]
    public async Task Fremdes_Geraet_lesen_aendern_loeschen_gibt_404()
    {
        var alice = factory.AsUser(TestData.UserId());
        var bob = factory.AsUser(TestData.UserId());
        var aliceDevice = await CreateDeviceAsync(alice, "Alice-Flur");

        var get = await bob.GetAsync($"/api/devices/{aliceDevice.Id}");
        var put = await bob.PutAsJsonAsync($"/api/devices/{aliceDevice.Id}", new DeviceUpdateDto("Gekapert", null, null));
        var delete = await bob.DeleteAsync($"/api/devices/{aliceDevice.Id}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        var unveraendert = await alice.GetFromJsonAsync<Device>($"/api/devices/{aliceDevice.Id}");
        Assert.Equal("Alice-Flur", unveraendert!.Name);
    }

    [Fact]
    public async Task Eigenes_Geraet_aendern_und_loeschen()
    {
        var client = factory.AsUser(TestData.UserId());
        var device = await CreateDeviceAsync(client, "Alt");

        var put = await client.PutAsJsonAsync($"/api/devices/{device.Id}",
            new DeviceUpdateDto("Neu", "Keller", "Unter der Treppe"));
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var geaendert = await client.GetFromJsonAsync<Device>($"/api/devices/{device.Id}");
        Assert.Equal("Neu", geaendert!.Name);
        Assert.Equal("Keller", geaendert.Standort);
        Assert.Equal("Unter der Treppe", geaendert.Caption);

        var delete = await client.DeleteAsync($"/api/devices/{device.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/devices/{device.Id}")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_zeigt_letzten_Messwert_pro_Geraet()
    {
        var client = factory.AsUser(TestData.UserId());
        var customerId = await GetCustomerIdAsync(client);
        var hw = TestData.HardwareId();
        var sensor = factory.CreateClient();

        await sensor.PostAsJsonAsync("/api/sensor", new { hardwareId = hw, customer_id = customerId, deviceName = "Bad", temperatur = 20.0, relativeFeuchte = 50.0 });
        await sensor.PostAsJsonAsync("/api/sensor", new { hardwareId = hw, customer_id = customerId, deviceName = "Bad", temperatur = 23.5, relativeFeuchte = 55.0, wasserAlarm = true });

        var dashboard = await client.GetFromJsonAsync<List<DeviceDashboardDto>>("/api/devices/dashboard");

        var kachel = Assert.Single(dashboard!);
        Assert.Equal(hw, kachel.HardwareId);
        Assert.Equal(23.5, kachel.Temperatur);
        Assert.Equal(55.0, kachel.RelativeFeuchte);
        Assert.True(kachel.Wassertank);
        Assert.NotNull(kachel.ZuletztGemeldet);
    }

    private record MeResponse(string CustomerId);
}
