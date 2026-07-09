using IotWelt.API.Data;
using IotWelt.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class SensorController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Report(SensorDataDto data)
    {
        Device? device;

        if (!string.IsNullOrEmpty(data.CustomerId))
        {
            // Neuer Modus: Device per CustomerId + DeviceName suchen
            device = await db.Devices.FirstOrDefaultAsync(
                d => d.CustomerId == data.CustomerId && d.Name == data.DeviceName);

            if (device is null)
            {
                // Auto-Create: neues Device für diesen Kunden anlegen
                device = new Device
                {
                    Name = data.DeviceName,
                    CustomerId = data.CustomerId,
                    ZuerstGesehen = DateTime.UtcNow
                };
                db.Devices.Add(device);
                await db.SaveChangesAsync();
            }
        }
        else
        {
            // Legacy-Modus (Übergang): Suche nur per DeviceName, kein CustomerId-Check
            device = await db.Devices.FirstOrDefaultAsync(d => d.Name == data.DeviceName);
            if (device is null)
            {
                device = new Device { Name = data.DeviceName, ZuerstGesehen = DateTime.UtcNow };
                db.Devices.Add(device);
                await db.SaveChangesAsync();
            }
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
