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

        var owners = await Owners
            .Where(o => customerIds.Contains(o.CustomerId))
            .ToListAsync();

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

    // Übergangsweise im alten Format (CustomerProfileDto) — wird in PR 1c durch Konten/Logins ersetzt
    [HttpGet("customers")]
    public async Task<ActionResult<List<CustomerProfileDto>>> GetCustomers()
    {
        var owners = await Owners
            .OrderBy(o => o.DisplayName ?? o.Email ?? o.UserId)
            .Select(o => new CustomerProfileDto(o.UserId, o.CustomerId, o.DisplayName, o.Email))
            .ToListAsync();

        return Ok(owners);
    }

    // Löscht alle Konten, deren Owner dieser Login ist, samt Geräten (Messwerte per Cascade).
    // Der Login selbst bleibt bestehen — Login-Verwaltung folgt in PR 1c.
    [HttpDelete("customers/{ownerId}")]
    public async Task<IActionResult> DeleteCustomer(string ownerId)
    {
        var accounts = await db.Accounts
            .Where(a => a.Memberships.Any(m => m.UserId == ownerId && m.Role == AccountRole.Owner))
            .ToListAsync();
        if (accounts.Count == 0)
            return NotFound();

        var customerIds = accounts.Select(a => a.CustomerId).ToList();
        var devices = await db.Devices
            .Where(d => d.CustomerId != null && customerIds.Contains(d.CustomerId))
            .ToListAsync();

        db.Devices.RemoveRange(devices);
        db.Accounts.RemoveRange(accounts);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // Owner je Konto (genau einer pro Konto) — ersetzt die frühere Tabelle CustomerProfiles
    private IQueryable<OwnerInfo> Owners => db.AccountMemberships
        .Where(m => m.Role == AccountRole.Owner)
        .Select(m => new OwnerInfo(m.Account.CustomerId, m.UserId, m.User.DisplayName, m.User.Email));

    private record OwnerInfo(string CustomerId, string UserId, string? DisplayName, string? Email);
}
