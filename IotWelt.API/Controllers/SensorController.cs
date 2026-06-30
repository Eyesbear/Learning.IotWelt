using IotWelt.API.Data;
using IotWelt.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace IotWelt.API.Controllers;

[Authorize(Policy = "SensorWrite")]
[ApiController]
[Route("api/[controller]")]
public class SensorController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Report(SensorDataDto data)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Name == data.DeviceName);
        if (device is null)
        {
            device = new Device { Name = data.DeviceName, ZuerstGesehen = DateTime.UtcNow };
            db.Devices.Add(device);
            await db.SaveChangesAsync();
        }

        db.RaumKlimaLogs.Add(new RaumKlimaLog
        {
            DeviceId = device.Id,
            Temperatur = data.Temperatur,
            RelativeFeuchte = data.RelativeFeuchte,
            Wassertank = data.WasserAlarm,
            Zeitstempel = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return Ok();
    }
}
