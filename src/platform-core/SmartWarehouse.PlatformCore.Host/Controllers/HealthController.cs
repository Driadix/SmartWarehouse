using Microsoft.AspNetCore.Mvc;

namespace SmartWarehouse.PlatformCore.Host.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
  [HttpGet]
  public IActionResult Get()
  {
    return Ok(new
    {
      status = "ok",
      service = "platform-core"
    });
  }
}
