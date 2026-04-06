using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartWarehouse.PlatformCore.Host.HealthChecks;

namespace SmartWarehouse.PlatformCore.Host.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
  [HttpGet]
  public async Task<IActionResult> Get(CancellationToken cancellationToken)
  {
    var report = await healthCheckService.CheckHealthAsync(
        registration => registration.Tags.Contains("ready"),
        cancellationToken);

    var statusCode = report.Status == HealthStatus.Unhealthy
        ? StatusCodes.Status503ServiceUnavailable
        : StatusCodes.Status200OK;

    return StatusCode(statusCode, HealthReportResponseWriter.CreatePayload(report));
  }
}
