using IotWelt.API.Data;
using IotWelt.API.Services;
using IotWelt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RaumKlimaLogController(AppDbContext db, CustomerService customers) : ControllerBase
{
    [HttpGet("{deviceId:int}")]
    public async Task<ActionResult<IEnumerable<KlimaVerlaufPunkt>>> GetVerlauf(
        int deviceId,
        [FromQuery] int minutes = 60,
        [FromQuery] string? range = null)
    {
        var customerId = await customers.EnsureCustomerIdAsync(User);

        var deviceExists = await db.Devices.AnyAsync(
            d => d.Id == deviceId && d.CustomerId == customerId);
        if (!deviceExists)
            return NotFound();

        // Effektives Zeitfenster [fromUtc, toUtc) bestimmen
        DateTime fromUtc, toUtc;
        if (range is not null)
        {
            // Ganze Kalendertage – Grenzen in lokaler Zeit bestimmen, dann nach UTC umrechnen
            var (from, to) = ResolveRange(range);
            fromUtc = from.ToUniversalTime();
            toUtc = (to ?? DateTime.Now).ToUniversalTime();
        }
        else
        {
            toUtc = DateTime.UtcNow;
            fromUtc = toUtc.AddMinutes(-minutes);
        }

        var rows = await db.RaumKlimaLogs
            .Where(l => l.DeviceId == deviceId && l.Zeitstempel >= fromUtc && l.Zeitstempel < toUtc)
            .OrderBy(l => l.Zeitstempel)
            .Select(l => new { l.Zeitstempel, l.Temperatur, l.RelativeFeuchte })
            .ToListAsync();

        var bucketMinutes = ResolveBucketMinutes(toUtc - fromUtc);

        if (bucketMinutes <= 0)
        {
            // Kurze Fenster: Rohwerte ohne Aggregation
            return Ok(rows.Select(l => new KlimaVerlaufPunkt
            {
                Zeitstempel = l.Zeitstempel,
                Temperatur = l.Temperatur,
                RelativeFeuchte = l.RelativeFeuchte
            }));
        }

        // Längere Fenster: auf Zeit-Buckets mitteln, damit der Chart lesbar/performant bleibt
        var bucketTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
        var data = rows
            .GroupBy(l => new DateTime(l.Zeitstempel.Ticks - (l.Zeitstempel.Ticks % bucketTicks), DateTimeKind.Utc))
            .Select(g => new KlimaVerlaufPunkt
            {
                Zeitstempel = g.Key,
                Temperatur = AvgOrNull(g.Select(x => x.Temperatur)),
                RelativeFeuchte = AvgOrNull(g.Select(x => x.RelativeFeuchte))
            })
            .OrderBy(p => p.Zeitstempel)
            .ToList();

        return Ok(data);
    }

    // Kalendertag-Presets: From (inklusive) .. To (exklusiv) in lokaler Zeit
    private static (DateTime From, DateTime? To) ResolveRange(string range) => range switch
    {
        "today" => (DateTime.Today, null),
        "yesterday" => (DateTime.Today.AddDays(-1), DateTime.Today),
        "7days" => (DateTime.Today.AddDays(-6), null),
        _ => (DateTime.UtcNow.AddMinutes(-60), null)
    };

    // Bucket-Größe an die Fensterdauer koppeln (Anker: 24 h → 15 min = 4/h, 7 Tage → 60 min = 1/h)
    private static int ResolveBucketMinutes(TimeSpan duration) => duration.TotalHours switch
    {
        <= 4 => 0,     // Rohdaten
        <= 12 => 5,
        <= 24 => 15,
        <= 48 => 30,
        _ => 60
    };

    private static double? AvgOrNull(IEnumerable<double?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : Math.Round(present.Average(), 1);
    }
}
