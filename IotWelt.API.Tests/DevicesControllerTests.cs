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
        var user = await factory.CreateUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/devices",
            new { name = "Wohnzimmer", customerId = TestData.CustomerId() });
        var device = await response.Content.ReadFromJsonAsync<Device>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(user.CustomerId, device!.CustomerId);
    }

    [Fact]
    public async Task Kunde_sieht_nur_eigene_Geraete()
    {
        var alice = (await factory.CreateUserAsync()).Client;
        var bob = (await factory.CreateUserAsync()).Client;
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
        var alice = (await factory.CreateUserAsync()).Client;
        var bob = (await factory.CreateUserAsync()).Client;
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
        var client = (await factory.CreateUserAsync()).Client;
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
        var user = await factory.CreateUserAsync();
        var hw = TestData.HardwareId();
        var sensor = factory.CreateClient();

        await sensor.PostAsJsonAsync("/api/sensor", new { hardwareId = hw, customer_id = user.CustomerId, deviceName = "Bad", temperatur = 20.0, relativeFeuchte = 50.0 });
        await sensor.PostAsJsonAsync("/api/sensor", new { hardwareId = hw, customer_id = user.CustomerId, deviceName = "Bad", temperatur = 23.5, relativeFeuchte = 55.0, wasserAlarm = true });

        var dashboard = await user.Client.GetFromJsonAsync<List<DeviceDashboardDto>>("/api/devices/dashboard");

        var kachel = Assert.Single(dashboard!);
        Assert.Equal(hw, kachel.HardwareId);
        Assert.Equal(23.5, kachel.Temperatur);
        Assert.Equal(55.0, kachel.RelativeFeuchte);
        Assert.True(kachel.Wassertank);
        Assert.NotNull(kachel.ZuletztGemeldet);
    }

    // --- Rechte-Matrix (docs/user-stories/benutzerverwaltung.md) ---

    [Fact]
    public async Task Reader_sieht_Geraete_des_Kontos_darf_aber_nichts_aendern()
    {
        var owner = await factory.CreateUserAsync();
        var device = await CreateDeviceAsync(owner.Client, "Geteilt");
        var gast = await factory.CreateUserAsync();
        await factory.AddMemberAsync(owner.CustomerId, gast.UserId, AccountRole.Reader);

        var reader = await factory.SwitchToAsync(gast, owner.CustomerId);

        var liste = await reader.Client.GetFromJsonAsync<List<Device>>("/api/devices");
        Assert.Equal(device.Id, Assert.Single(liste!).Id);

        var post = await reader.Client.PostAsJsonAsync("/api/devices", new { name = "Neu" });
        var put = await reader.Client.PutAsJsonAsync($"/api/devices/{device.Id}", new DeviceUpdateDto("Gekapert", null, null));
        var delete = await reader.Client.DeleteAsync($"/api/devices/{device.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Editor_darf_aendern_aber_nur_Owner_darf_loeschen()
    {
        var owner = await factory.CreateUserAsync();
        var device = await CreateDeviceAsync(owner.Client, "Geteilt");
        var helfer = await factory.CreateUserAsync();
        await factory.AddMemberAsync(owner.CustomerId, helfer.UserId, AccountRole.Editor);

        var editor = await factory.SwitchToAsync(helfer, owner.CustomerId);

        var put = await editor.Client.PutAsJsonAsync($"/api/devices/{device.Id}", new DeviceUpdateDto("Umbenannt", null, null));
        var delete = await editor.Client.DeleteAsync($"/api/devices/{device.Id}");

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Login_ohne_Konto_sieht_keine_herrenlosen_Geraete()
    {
        // Legacy-Gerät ohne CustomerId — darf niemals bei einem Login ohne Konto landen
        var hw = TestData.HardwareId();
        await factory.CreateClient().PostAsJsonAsync("/api/sensor", new { hardwareId = hw, deviceName = TestData.DeviceName(), temperatur = 20.0 });

        var user = await factory.CreateUserAsync();
        await factory.WithDbAsync(async db =>
        {
            db.Accounts.RemoveRange(db.Accounts.Where(a => a.CustomerId == user.CustomerId));
            await db.SaveChangesAsync();
        });
        var ohneKonto = await factory.LoginAsync(user.Email);

        var response = await ohneKonto.Client.GetAsync("/api/devices");

        Assert.Equal(string.Empty, ohneKonto.CustomerId);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
