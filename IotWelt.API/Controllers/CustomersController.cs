using IotWelt.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotWelt.API.Controllers;

[Authorize]
[ApiController]
[Route("api/customers")]
public class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<object>> GetMe()
    {
        var customerId = await customers.EnsureCustomerIdAsync(User);
        if (customerId is null) return Unauthorized();
        return Ok(new { customerId });
    }
}
