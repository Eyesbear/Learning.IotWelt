using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DevicesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Device>>> GetAll()
        => await db.Devices.ToListAsync();

    [HttpGet("dashboard")]
    public async Task<ActionResult<IEnumerable<DeviceDashboardDto>>> GetDashboard()
    {
        var devices = await db.Devices.ToListAsync();

        var maxIdPerDevice = await db.RaumKlimaLogs
            .GroupBy(l => l.DeviceId)
            .Select(g => g.Max(l => l.Id))
            .ToListAsync();

        var latestLogs = await db.RaumKlimaLogs
            .Where(l => maxIdPerDevice.Contains(l.Id))
            .ToListAsync();

        var result = devices.Select(d =>
        {
            var log = latestLogs.FirstOrDefault(l => l.DeviceId == d.Id);
            return new DeviceDashboardDto
            {
                Id = d.Id,
                Name = d.Name,
                Typ = d.Typ,
                Standort = d.Standort,
                ZuerstGesehen = d.ZuerstGesehen,
                Temperatur = log?.Temperatur,
                RelativeFeuchte = log?.RelativeFeuchte,
                Wassertank = log?.Wassertank,
                ZuletztGemeldet = log?.Zeitstempel
            };
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Device>> GetById(int id)
    {
        var device = await db.Devices.FindAsync(id);
        return device is null ? NotFound() : Ok(device);
    }

    [HttpPost]
    public async Task<ActionResult<Device>> Create(Device device)
    {
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Device device)
    {
        if (id != device.Id)
            return BadRequest();

        db.Entry(device).State = EntityState.Modified;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await db.Devices.AnyAsync(d => d.Id == id))
                return NotFound();
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var device = await db.Devices.FindAsync(id);
        if (device is null)
            return NotFound();

        db.Devices.Remove(device);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
