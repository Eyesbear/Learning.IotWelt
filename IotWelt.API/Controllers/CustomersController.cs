using IotWelt.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotWelt.API.Controllers;

[Authorize]
[ApiController]
[Route("api/customers")]
public class CustomersController(CurrentAccount current) : ControllerBase
{
    // Kennung des aktiven Kontos — wird im ESP32 als customer_id konfiguriert
    [HttpGet("me")]
    public ActionResult<object> GetMe()
    {
        var customerId = current.CustomerId;
        if (customerId is null) return Unauthorized();
        return Ok(new { customerId });
    }
}
