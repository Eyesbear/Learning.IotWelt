using IotWelt.API.Data;
using IotWelt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RaumKlimaLogController(AppDbContext db) : ControllerBase
{
    [HttpGet("{deviceId:int}")]
    public async Task<ActionResult<IEnumerable<KlimaVerlaufPunkt>>> GetVerlauf(int deviceId, [FromQuery] int minutes = 60)
    {
        var seit = DateTime.UtcNow.AddMinutes(-minutes);

        var data = await db.RaumKlimaLogs
            .Where(l => l.DeviceId == deviceId && l.Zeitstempel >= seit)
            .OrderBy(l => l.Zeitstempel)
            .Select(l => new KlimaVerlaufPunkt
            {
                Zeitstempel = l.Zeitstempel,
                Temperatur = l.Temperatur,
                RelativeFeuchte = l.RelativeFeuchte
            })
            .ToListAsync();

        return Ok(data);
    }
}
