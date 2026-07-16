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

        if (!string.IsNullOrEmpty(data.HardwareId))
        {
            // Primärer Modus: Gerät per HardwareId identifizieren
            device = await db.Devices.FirstOrDefaultAsync(d => d.HardwareId == data.HardwareId);

            if (device is null)
            {
                device = new Device
                {
                    Name = data.DeviceName,
                    HardwareId = data.HardwareId,
                    CustomerId = data.CustomerId,
                    ZuerstGesehen = DateTime.UtcNow
                };
                db.Devices.Add(device);
                await db.SaveChangesAsync();
            }
            else
            {
                // Gerät bekannt (per HardwareId): Stammdaten ggf. an gemeldete Werte angleichen
                var changed = false;

                if (!string.IsNullOrEmpty(data.DeviceName) && device.Name != data.DeviceName)
                {
                    device.Name = data.DeviceName;
                    changed = true;
                }

                if (!string.IsNullOrEmpty(data.CustomerId) && device.CustomerId != data.CustomerId)
                {
                    device.CustomerId = data.CustomerId;
                    changed = true;
                }

                if (changed)
                    await db.SaveChangesAsync();
            }
        }
        else if (!string.IsNullOrEmpty(data.CustomerId))
        {
            // Fallback: CustomerId + DeviceName (Übergangs-Modus)
            device = await db.Devices.FirstOrDefaultAsync(
                d => d.CustomerId == data.CustomerId && d.Name == data.DeviceName);

            if (device is null)
            {
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
            // Legacy-Modus: nur DeviceName, kein CustomerId/HardwareId
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
