using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Tests;

// Charakterisierungstests: halten das Verhalten von POST /api/sensor vor dem Auth-Umbau fest.
[Collection(ApiCollection.Name)]
public class SensorControllerTests(ApiFactory factory)
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    private Task<HttpResponseMessage> ReportAsync(object payload) =>
        _anonymous.PostAsJsonAsync("/api/sensor", payload);

    [Fact]
    public async Task Unbekannte_HardwareId_legt_Geraet_an_und_speichert_Messwert()
    {
        var hw = TestData.HardwareId();
        var customer = TestData.CustomerId();

        var response = await ReportAsync(new
        {
            hardwareId = hw, customer_id = customer, deviceName = "Keller",
            temperatur = 18.5, relativeFeuchte = 62.0, wasserAlarm = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var device = await factory.WithDbAsync(db => db.Devices.SingleAsync(d => d.HardwareId == hw));
        Assert.Equal("Keller", device.Name);
        Assert.Equal(customer, device.CustomerId);
        Assert.NotNull(device.ZuerstGesehen);

        var log = await factory.WithDbAsync(db => db.RaumKlimaLogs.SingleAsync(l => l.DeviceId == device.Id));
        Assert.Equal(18.5, log.Temperatur);
        Assert.Equal(62.0, log.RelativeFeuchte);
        Assert.True(log.Wassertank);
    }

    [Fact]
    public async Task Bekannte_HardwareId_aktualisiert_Name_und_legt_kein_zweites_Geraet_an()
    {
        var hw = TestData.HardwareId();
        var customer = TestData.CustomerId();

        await ReportAsync(new { hardwareId = hw, customer_id = customer, deviceName = "Alt", temperatur = 20.0 });
        await ReportAsync(new { hardwareId = hw, customer_id = customer, deviceName = "Neu", temperatur = 21.0 });

        var devices = await factory.WithDbAsync(db => db.Devices.Where(d => d.HardwareId == hw).ToListAsync());
        var device = Assert.Single(devices);
        Assert.Equal("Neu", device.Name);

        var logCount = await factory.WithDbAsync(db => db.RaumKlimaLogs.CountAsync(l => l.DeviceId == device.Id));
        Assert.Equal(2, logCount);
    }

    [Fact]
    public async Task Bekannte_HardwareId_mit_anderer_CustomerId_wechselt_den_Besitzer()
    {
        // ACHTUNG — dokumentiert eine Sicherheitslücke des Ist-Zustands:
        // Der Endpoint ist anonym; wer eine HardwareId kennt, kann das Gerät einem anderen Kunden zuordnen.
        // Wird in Phase 1 (Geräte-Schlüssel) geschlossen — dann muss dieser Test angepasst werden.
        var hw = TestData.HardwareId();
        var original = TestData.CustomerId();
        var angreifer = TestData.CustomerId();

        await ReportAsync(new { hardwareId = hw, customer_id = original, deviceName = "Bad" });
        await ReportAsync(new { hardwareId = hw, customer_id = angreifer, deviceName = "Bad" });

        var device = await factory.WithDbAsync(db => db.Devices.SingleAsync(d => d.HardwareId == hw));
        Assert.Equal(angreifer, device.CustomerId);
    }

    [Fact]
    public async Task Ohne_HardwareId_wird_ueber_CustomerId_und_Name_zugeordnet()
    {
        var customer = TestData.CustomerId();
        var name = TestData.DeviceName();

        await ReportAsync(new { customer_id = customer, deviceName = name, temperatur = 19.0 });
        await ReportAsync(new { customer_id = customer, deviceName = name, temperatur = 19.5 });

        var devices = await factory.WithDbAsync(db =>
            db.Devices.Where(d => d.CustomerId == customer && d.Name == name).ToListAsync());
        var device = Assert.Single(devices);
        Assert.Null(device.HardwareId);
    }

    [Fact]
    public async Task Legacy_Modus_nur_mit_Namen_legt_Geraet_ohne_Kunde_an()
    {
        var name = TestData.DeviceName();

        var response = await ReportAsync(new { deviceName = name, temperatur = 22.0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var device = await factory.WithDbAsync(db => db.Devices.SingleAsync(d => d.Name == name));
        Assert.Null(device.CustomerId);
    }
}
