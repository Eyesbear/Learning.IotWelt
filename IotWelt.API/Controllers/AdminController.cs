using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

[Authorize(Roles = Policies.AdminRole)]
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

        var owners = await Owners(customerIds).ToListAsync();

        var items = devices.Select(d =>
        {
            var owner = owners.FirstOrDefault(o => o.CustomerId == d.CustomerId);
            return new AdminDeviceDto(
                d.Id, d.Name, d.Caption, d.Standort, d.Typ, d.DeviceId, d.HardwareId,
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

    // Owner je Konto (genau einer pro Konto) — ersetzt die frühere Tabelle CustomerProfiles.
    // Filtern und Sortieren VOR dem Select: auf Eigenschaften eines per Konstruktor erzeugten
    // Records kann EF Core nicht mehr in SQL übersetzen.
    private IQueryable<OwnerInfo> Owners(List<string> customerIds)
    {
        var owners = db.AccountMemberships
            .Where(m => m.Role == AccountRole.Owner && customerIds.Contains(m.Account.CustomerId));

        return owners
            .OrderBy(m => m.User.DisplayName ?? m.User.Email ?? m.UserId)
            .Select(m => new OwnerInfo(m.Account.CustomerId, m.UserId, m.User.DisplayName, m.User.Email));
    }

    private record OwnerInfo(string CustomerId, string UserId, string? DisplayName, string? Email);
}
