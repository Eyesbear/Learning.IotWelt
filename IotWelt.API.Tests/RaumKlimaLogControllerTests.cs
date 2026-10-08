using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Models;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common;

namespace IotWelt.API.Tests;

// Verlauf-Endpoint: Ownership-Prüfung, Zeitfenster und serverseitige Aggregation in Buckets.
[Collection(ApiCollection.Name)]
public class RaumKlimaLogControllerTests(ApiFactory factory)
{
    // Legt für den angemeldeten Benutzer ein Gerät mit Messwerten zu frei wählbaren Zeitpunkten an
    private async Task<int> SeedDeviceAsync(HttpClient owner, params (DateTime Zeit, double Temp)[] logs)
    {
        var response = await owner.PostAsJsonAsync("/api/devices", new { name = "Verlauf" });
        var device = (await response.Content.ReadFromJsonAsync<Device>())!;

        await factory.WithDbAsync(async db =>
        {
            db.RaumKlimaLogs.AddRange(logs.Select(l => new RaumKlimaLog
            {
                DeviceId = device.Id, Zeitstempel = l.Zeit, Temperatur = l.Temp, RelativeFeuchte = 50
            }));
            await db.SaveChangesAsync();
        });

        return device.Id;
    }

    private static DateTime FloorTo(DateTime t, TimeSpan bucket) =>
        new(t.Ticks - t.Ticks % bucket.Ticks, DateTimeKind.Utc);

    [Fact]
    public async Task Verlauf_eines_fremden_Geraets_gibt_404()
    {
        var alice = (await factory.CreateUserAsync()).Client;
        var bob = (await factory.CreateUserAsync()).Client;
        var deviceId = await SeedDeviceAsync(alice, (DateTime.UtcNow.AddMinutes(-5), 20));

        var response = await bob.GetAsync($"/api/raumklimalog/{deviceId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Kurzes_Fenster_liefert_Rohwerte_nur_innerhalb_des_Fensters()
    {
        var client = (await factory.CreateUserAsync()).Client;
        var now = DateTime.UtcNow;
        var deviceId = await SeedDeviceAsync(client,
            (now.AddMinutes(-90), 10),   // außerhalb von 60 min
            (now.AddMinutes(-30), 20),
            (now.AddMinutes(-10), 21));

        var punkte = await client.GetFromJsonAsync<List<KlimaVerlaufPunkt>>($"/api/raumklimalog/{deviceId}?minutes=60");

        Assert.Equal([20.0, 21.0], punkte!.Select(p => p.Temperatur!.Value));
    }

    [Fact]
    public async Task Fenster_von_12_Stunden_mittelt_auf_5_Minuten_Buckets()
    {
        var client = (await factory.CreateUserAsync()).Client;
        var bucket = TimeSpan.FromMinutes(5);
        var start = FloorTo(DateTime.UtcNow.AddHours(-2), bucket);
        var deviceId = await SeedDeviceAsync(client,
            (start.AddMinutes(1), 20.0),
            (start.AddMinutes(3), 22.4),   // gleicher Bucket → Mittelwert 21.2
            (start.AddMinutes(6), 30.0));  // nächster Bucket

        var punkte = await client.GetFromJsonAsync<List<KlimaVerlaufPunkt>>($"/api/raumklimalog/{deviceId}?minutes=720");

        Assert.Equal(2, punkte!.Count);
        Assert.Equal(start, punkte[0].Zeitstempel);
        Assert.Equal(21.2, punkte[0].Temperatur);
        Assert.Equal(start.Add(bucket), punkte[1].Zeitstempel);
        Assert.Equal(30.0, punkte[1].Temperatur);
    }
}
