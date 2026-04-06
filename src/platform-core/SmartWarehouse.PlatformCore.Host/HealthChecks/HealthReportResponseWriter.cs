using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmartWarehouse.PlatformCore.Host.HealthChecks;

internal static class HealthReportResponseWriter
{
  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

  public static Task WriteAsync(HttpContext context, HealthReport report)
  {
    context.Response.ContentType = "application/json; charset=utf-8";
    return context.Response.WriteAsync(JsonSerializer.Serialize(CreatePayload(report), SerializerOptions));
  }

  public static object CreatePayload(HealthReport report)
  {
    return new
    {
      status = report.Status.ToString().ToLowerInvariant(),
      timestampUtc = DateTimeOffset.UtcNow,
      totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
      entries = report.Entries.ToDictionary(
          entry => entry.Key,
          entry => new
          {
            status = entry.Value.Status.ToString().ToLowerInvariant(),
            description = entry.Value.Description,
            durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
            tags = entry.Value.Tags,
            data = entry.Value.Data
          })
    };
  }
}
