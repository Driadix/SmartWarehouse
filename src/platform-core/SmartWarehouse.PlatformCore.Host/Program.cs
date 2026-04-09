using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SmartWarehouse.PlatformCore.Application.Wes;
using SmartWarehouse.PlatformCore.Host.HealthChecks;
using SmartWarehouse.PlatformCore.Host.Northbound;
using SmartWarehouse.PlatformCore.Host.Topology;
using SmartWarehouse.PlatformCore.Infrastructure.Northbound;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Wcs;

namespace SmartWarehouse.PlatformCore.Host;

public class Program
{
  private static readonly string[] ReadyTags = ["ready"];

  public static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "platform-core";
    var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0-dev";
    builder.Services
        .AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
          options.InvalidModelStateResponseFactory = context =>
          {
            var detail = string.Join(
                " ",
                context.ModelState.Values
                    .SelectMany(static entry => entry.Errors)
                    .Select(static error => error.ErrorMessage)
                    .Where(static message => !string.IsNullOrWhiteSpace(message)));

            return new BadRequestObjectResult(
                new NorthboundProblemResponse(
                    code: "INVALID_REQUEST",
                    title: "Некорректный формат запроса",
                    detail: string.IsNullOrWhiteSpace(detail) ? null : detail,
                    instance: context.HttpContext.Request.Path.Value));
          };
        });

    builder.Services.AddPlatformCorePersistence(builder.Configuration);
    builder.Services.AddConfiguredWarehouseTopology(builder.Configuration, builder.Environment);
    builder.Services.AddWarehouseRouteService();
    builder.Services.AddPayloadTransferJobPlanner();
    builder.Services.AddPayloadTransferJobService();
    builder.Services.AddPersistenceWcsOperationalStateStore();

    builder.Logging.AddOpenTelemetry(options =>
    {
      options.IncludeFormattedMessage = true;
      options.IncludeScopes = true;

      if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var exporterEndpoint))
      {
        options.AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = exporterEndpoint);
      }
    });

    builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
        .WithMetrics(metrics =>
        {
          metrics
              .AddAspNetCoreInstrumentation()
              .AddMeter("Microsoft.AspNetCore.Hosting")
              .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
              .AddMeter("System.Net.Http")
              .AddMeter("System.Runtime");

          if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var exporterEndpoint))
          {
            metrics.AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = exporterEndpoint);
          }
        })
        .WithTracing(tracing =>
        {
          tracing
              .AddAspNetCoreInstrumentation()
              .AddHttpClientInstrumentation();

          if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var exporterEndpoint))
          {
            tracing.AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = exporterEndpoint);
          }
        });

    var healthChecks = builder.Services.AddHealthChecks();
    RegisterTcpDependencyHealthCheck(
        healthChecks,
        name: "postgres",
        options: builder.Configuration.GetSection("HealthChecks:Dependencies:Postgres").Get<DependencyHealthCheckOptions>());
    RegisterTcpDependencyHealthCheck(
        healthChecks,
        name: "nats",
        options: builder.Configuration.GetSection("HealthChecks:Dependencies:Nats").Get<DependencyHealthCheckOptions>());

    var app = builder.Build();

    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/healthz", CreateHealthCheckOptions());
    app.MapHealthChecks(
        "/health/live",
        CreateHealthCheckOptions(predicate: _ => false));
    app.MapHealthChecks(
        "/health/ready",
        CreateHealthCheckOptions(predicate: registration => registration.Tags.Contains("ready")));

    app.Run();
  }

  private static HealthCheckOptions CreateHealthCheckOptions(Func<HealthCheckRegistration, bool>? predicate = null)
  {
    var options = new HealthCheckOptions
    {
      Predicate = predicate,
      ResponseWriter = HealthReportResponseWriter.WriteAsync
    };

    options.ResultStatusCodes[HealthStatus.Healthy] = StatusCodes.Status200OK;
    options.ResultStatusCodes[HealthStatus.Degraded] = StatusCodes.Status200OK;
    options.ResultStatusCodes[HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable;

    return options;
  }

  private static void RegisterTcpDependencyHealthCheck(
      IHealthChecksBuilder healthChecks,
      string name,
      DependencyHealthCheckOptions? options)
  {
    if (options is null || !options.Enabled || string.IsNullOrWhiteSpace(options.Host))
    {
      return;
    }

    healthChecks.AddCheck(
        name,
        new TcpDependencyHealthCheck(name, options.Host, options.Port, TimeSpan.FromSeconds(options.TimeoutSeconds)),
        tags: ReadyTags);
  }
}
