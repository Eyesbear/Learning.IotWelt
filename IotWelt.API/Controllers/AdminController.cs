using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin")]
public class AdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("devices")]
    public async Task<ActionResult<PagedResult<AdminDeviceDto>>> GetDevices(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? customerId = null)
    {
        var query = db.Devices.AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerId))
            query = query.Where(d => d.CustomerId == customerId);

        var total = await query.CountAsync();

        var devices = await query
            .OrderBy(d => d.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        var customerIds = devices
            .Where(d => d.CustomerId != null)
            .Select(d => d.CustomerId!)
            .Distinct()
            .ToList();

        var profiles = await db.CustomerProfiles
            .Where(p => customerIds.Contains(p.CustomerId))
            .ToListAsync();

        var items = devices.Select(d =>
        {
            var owner = profiles.FirstOrDefault(p => p.CustomerId == d.CustomerId);
            return new AdminDeviceDto(
                d.Id, d.Name, d.Caption, d.Standort, d.Typ, d.DeviceId,
                d.CustomerId, d.ZuerstGesehen,
                owner?.DisplayName, owner?.Email);
        }).ToList();

        return Ok(new PagedResult<AdminDeviceDto>(items, total));
    }

    [HttpPut("devices/{id}")]
    public async Task<IActionResult> UpdateDevice(int id, AdminDeviceUpdateDto dto)
    {
        var device = await db.Devices.FindAsync(id);
        if (device is null)
            return NotFound();

        device.Name = dto.Name;
        device.Standort = dto.Standort;
        device.Caption = dto.Caption;
        device.CustomerId = dto.CustomerId;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("devices/{id}")]
    public async Task<IActionResult> DeleteDevice(int id)
    {
        var device = await db.Devices.FindAsync(id);
        if (device is null)
            return NotFound();

        db.Devices.Remove(device);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("customers")]
    public async Task<ActionResult<List<CustomerProfileDto>>> GetCustomers()
    {
        var profiles = await db.CustomerProfiles
            .OrderBy(p => p.DisplayName ?? p.Email ?? p.OwnerId)
            .Select(p => new CustomerProfileDto(p.OwnerId, p.CustomerId, p.DisplayName, p.Email))
            .ToListAsync();

        return Ok(profiles);
    }

    [HttpDelete("customers/{ownerId}")]
    public async Task<IActionResult> DeleteCustomer(string ownerId)
    {
        var profile = await db.CustomerProfiles
            .FirstOrDefaultAsync(p => p.OwnerId == ownerId);
        if (profile is null)
            return NotFound();

        var devices = await db.Devices
            .Where(d => d.CustomerId == profile.CustomerId)
            .ToListAsync();

        db.Devices.RemoveRange(devices);
        db.CustomerProfiles.Remove(profile);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
