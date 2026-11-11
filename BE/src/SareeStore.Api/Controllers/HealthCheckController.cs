using Microsoft.AspNetCore.Mvc;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthCheckController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "SareeStore.Api",
            TimestampUtc = DateTime.UtcNow
        });
    }
}
