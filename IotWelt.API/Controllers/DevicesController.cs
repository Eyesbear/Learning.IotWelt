using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

[Authorize(Policy = Policies.CanRead)]
[ApiController]
[Route("api/[controller]")]
public class DevicesController(AppDbContext db, CurrentAccount current) : ControllerBase
{
    private string? CustomerId => current.CustomerId;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Device>>> GetAll()
    {
        var customerId = CustomerId;
        return await db.Devices
            .Where(d => d.CustomerId == customerId)
            .ToListAsync();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<IEnumerable<DeviceDashboardDto>>> GetDashboard()
    {
        var customerId = CustomerId;

        var devices = await db.Devices
            .Where(d => d.CustomerId == customerId)
            .ToListAsync();

        var deviceIds = devices.Select(d => d.Id).ToList();

        var maxIdPerDevice = await db.RaumKlimaLogs
            .Where(l => deviceIds.Contains(l.DeviceId))
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
                HardwareId = d.HardwareId,
                Typ = d.Typ,
                Standort = d.Standort,
                Caption = d.Caption,
                CustomerId = d.CustomerId,
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
        var customerId = CustomerId;
        var device = await db.Devices.FirstOrDefaultAsync(
            d => d.Id == id && d.CustomerId == customerId);
        return device is null ? NotFound() : Ok(device);
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<ActionResult<Device>> Create(Device device)
    {
        device.CustomerId = CustomerId;
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Update(int id, DeviceUpdateDto dto)
    {
        var customerId = CustomerId;
        var device = await db.Devices.FirstOrDefaultAsync(
            d => d.Id == id && d.CustomerId == customerId);
        if (device is null)
            return NotFound();

        device.Name = dto.Name;
        device.Standort = dto.Standort;
        device.Caption = dto.Caption;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.IsOwner)]
    public async Task<IActionResult> Delete(int id)
    {
        var customerId = CustomerId;
        var device = await db.Devices.FirstOrDefaultAsync(
            d => d.Id == id && d.CustomerId == customerId);
        if (device is null)
            return NotFound();

        db.Devices.Remove(device);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
