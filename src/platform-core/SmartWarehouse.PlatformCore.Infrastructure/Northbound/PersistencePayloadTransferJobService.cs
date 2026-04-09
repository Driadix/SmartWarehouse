using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWarehouse.PlatformCore.Application.Northbound;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wes;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;
using System.Security.Cryptography;
using System.Text.Json;

namespace SmartWarehouse.PlatformCore.Infrastructure.Northbound;

public static class PayloadTransferJobServiceCollectionExtensions
{
  public static IServiceCollection AddPayloadTransferJobService(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddScoped<IPayloadTransferJobService, PersistencePayloadTransferJobService>();

    return services;
  }
}

internal sealed class PersistencePayloadTransferJobService(
    PlatformCoreDbContext dbContext,
    CompiledWarehouseTopology topology,
    IWarehouseRouteService routeService) : IPayloadTransferJobService
{
  public async Task<CreatePayloadTransferJobResult> CreateAsync(
      CreatePayloadTransferJobCommand command,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var requestHash = ComputeRequestHash(command);
    var existingRegistration = await dbContext.NorthboundIdempotency
        .AsNoTracking()
        .SingleOrDefaultAsync(record => record.ClientOrderId == command.ClientOrderId, cancellationToken);

    if (existingRegistration is not null)
    {
      if (!string.Equals(existingRegistration.RequestHash, requestHash, StringComparison.Ordinal))
      {
        throw CreateIdempotencyConflict();
      }

      var existingJob = await FindJobByIdAsync(existingRegistration.JobId, cancellationToken);
      return new CreatePayloadTransferJobResult(MapToModel(existingJob), isIdempotentReplay: true);
    }

    if (command.SourceEndpointId == command.TargetEndpointId)
    {
      throw CreateIdenticalEndpoints();
    }

    EnsureEndpointExists(command.SourceEndpointId, isSource: true);
    EnsureEndpointExists(command.TargetEndpointId, isSource: false);

    try
    {
      routeService.ResolveRoute(topology, command.SourceEndpointId, command.TargetEndpointId);
    }
    catch (NoAdmissibleRouteException)
    {
      throw CreateNoAdmissibleRoute();
    }

    var now = DateTimeOffset.UtcNow;
    var jobRecord = new JobRecord
    {
      JobId = $"job-{Guid.NewGuid():N}",
      ClientOrderId = command.ClientOrderId,
      JobType = JobType.PayloadTransfer,
      PayloadId = null,
      SourceEndpointId = command.SourceEndpointId.Value,
      TargetEndpointId = command.TargetEndpointId.Value,
      State = JobState.Accepted,
      Priority = command.Priority,
      PayloadRef = SerializeOptionalJson(command.PayloadRef),
      Attributes = SerializeOptionalJson(command.Attributes),
      CreatedAt = now,
      UpdatedAt = now
    };

    dbContext.Jobs.Add(jobRecord);
    dbContext.NorthboundIdempotency.Add(new NorthboundIdempotencyRecord
    {
      ClientOrderId = command.ClientOrderId,
      RequestHash = requestHash,
      JobId = jobRecord.JobId,
      RegisteredAt = now
    });

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CreatePayloadTransferJobResult(MapToModel(jobRecord), isIdempotentReplay: false);
  }

  public async Task<PayloadTransferJobModel> GetByJobIdAsync(
      string jobId,
      CancellationToken cancellationToken = default)
  {
    var jobRecord = await FindJobByIdAsync(jobId, cancellationToken);
    return MapToModel(jobRecord);
  }

  public async Task<PayloadTransferJobModel> GetByClientOrderIdAsync(
      string clientOrderId,
      CancellationToken cancellationToken = default)
  {
    var normalizedClientOrderId = NormalizeRequired(clientOrderId, nameof(clientOrderId));
    var jobRecord = await dbContext.Jobs
        .AsNoTracking()
        .SingleOrDefaultAsync(record => record.ClientOrderId == normalizedClientOrderId, cancellationToken);

    return jobRecord is not null
        ? MapToModel(jobRecord)
        : throw CreateJobNotFound();
  }

  public async Task<CancelPayloadTransferJobResult> CancelAsync(
      string jobId,
      CancellationToken cancellationToken = default)
  {
    var normalizedJobId = NormalizeRequired(jobId, nameof(jobId));
    var jobRecord = await dbContext.Jobs
        .SingleOrDefaultAsync(record => record.JobId == normalizedJobId, cancellationToken);

    if (jobRecord is null)
    {
      throw CreateJobNotFound();
    }

    if (jobRecord.State == JobState.Cancelled)
    {
      return new CancelPayloadTransferJobResult(MapToModel(jobRecord), wasAlreadyCancelled: true);
    }

    if (jobRecord.State is JobState.Completed or JobState.Failed)
    {
      throw CreateCancelNotAllowed();
    }

    var now = DateTimeOffset.UtcNow;
    jobRecord.State = JobState.Cancelled;
    jobRecord.UpdatedAt = now;
    jobRecord.CompletedAt ??= now;

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CancelPayloadTransferJobResult(MapToModel(jobRecord), wasAlreadyCancelled: false);
  }

  private void EnsureEndpointExists(SmartWarehouse.PlatformCore.Domain.Primitives.EndpointId endpointId, bool isSource)
  {
    if (topology.TryResolveEndpoint(endpointId, out _))
    {
      return;
    }

    throw isSource ? CreateUnknownSourceEndpoint() : CreateUnknownTargetEndpoint();
  }

  private async Task<JobRecord> FindJobByIdAsync(string jobId, CancellationToken cancellationToken)
  {
    var normalizedJobId = NormalizeRequired(jobId, nameof(jobId));
    var jobRecord = await dbContext.Jobs
        .AsNoTracking()
        .SingleOrDefaultAsync(record => record.JobId == normalizedJobId, cancellationToken);

    return jobRecord ?? throw CreateJobNotFound();
  }

  private static PayloadTransferJobModel MapToModel(JobRecord record)
  {
    ArgumentNullException.ThrowIfNull(record);

    return new PayloadTransferJobModel(
        record.JobId,
        record.ClientOrderId,
        PayloadTransferJobContract.ToExternalState(record.State),
        record.SourceEndpointId,
        record.TargetEndpointId,
        PayloadTransferJobContract.ToExternalPriority(record.Priority),
        record.CreatedAt,
        record.UpdatedAt,
        DeserializeOptionalJson(record.PayloadRef),
        DeserializeOptionalJson(record.Attributes),
        record.ReasonCode is null ? null : new PayloadTransferJobReasonModel(record.ReasonCode, record.ReasonMessage),
        record.CompletedAt);
  }

  private static string ComputeRequestHash(CreatePayloadTransferJobCommand command)
  {
    using var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer))
    {
      writer.WriteStartObject();
      writer.WriteString("sourceEndpointId", command.SourceEndpointId.Value);
      writer.WriteString("targetEndpointId", command.TargetEndpointId.Value);
      writer.WriteString("priority", PayloadTransferJobContract.ToExternalPriority(command.Priority));
      writer.WritePropertyName("payloadRef");
      WriteNormalizedObject(writer, command.PayloadRef);
      writer.WritePropertyName("attributes");
      WriteNormalizedObject(writer, command.Attributes);
      writer.WriteEndObject();
    }

    var hash = SHA256.HashData(buffer.ToArray());
    return Convert.ToHexString(hash);
  }

  private static void WriteNormalizedObject(Utf8JsonWriter writer, JsonElement? element)
  {
    if (element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
    {
      writer.WriteStartObject();
      writer.WriteEndObject();
      return;
    }

    WriteCanonicalElement(writer, element.Value);
  }

  private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
  {
    switch (element.ValueKind)
    {
      case JsonValueKind.Object:
        writer.WriteStartObject();

        foreach (var property in element.EnumerateObject().OrderBy(static property => property.Name, StringComparer.Ordinal))
        {
          writer.WritePropertyName(property.Name);
          WriteCanonicalElement(writer, property.Value);
        }

        writer.WriteEndObject();
        break;
      case JsonValueKind.Array:
        writer.WriteStartArray();

        foreach (var item in element.EnumerateArray())
        {
          WriteCanonicalElement(writer, item);
        }

        writer.WriteEndArray();
        break;
      case JsonValueKind.String:
        writer.WriteStringValue(element.GetString());
        break;
      case JsonValueKind.Number:
        writer.WriteRawValue(element.GetRawText());
        break;
      case JsonValueKind.True:
      case JsonValueKind.False:
        writer.WriteBooleanValue(element.GetBoolean());
        break;
      case JsonValueKind.Null:
        writer.WriteNullValue();
        break;
      default:
        throw new InvalidOperationException($"Unsupported JSON token '{element.ValueKind}'.");
    }
  }

  private static string? SerializeOptionalJson(JsonElement? element)
  {
    if (element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
    {
      return null;
    }

    return element.Value.GetRawText();
  }

  private static JsonElement? DeserializeOptionalJson(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    return JsonSerializer.Deserialize<JsonElement>(value);
  }

  private static NorthboundProblemException CreateIdempotencyConflict() =>
      new(
          statusCode: 409,
          problemCode: "IDEMPOTENCY_CONFLICT",
          title: "Конфликт идемпотентности",
          detail: "Запрос с тем же `clientOrderId` уже существует, но нормализованное тело отличается.");

  private static NorthboundProblemException CreateUnknownSourceEndpoint() =>
      new(
          statusCode: 422,
          problemCode: "UNKNOWN_SOURCE_ENDPOINT",
          title: "Неизвестная исходная конечная точка",
          detail: "`sourceEndpointId` отсутствует в текущей конфигурации топологии.");

  private static NorthboundProblemException CreateUnknownTargetEndpoint() =>
      new(
          statusCode: 422,
          problemCode: "UNKNOWN_TARGET_ENDPOINT",
          title: "Неизвестная целевая конечная точка",
          detail: "`targetEndpointId` отсутствует в текущей конфигурации топологии.");

  private static NorthboundProblemException CreateIdenticalEndpoints() =>
      new(
          statusCode: 422,
          problemCode: "IDENTICAL_ENDPOINTS",
          title: "Совпадающие конечные точки",
          detail: "`sourceEndpointId` и `targetEndpointId` не должны совпадать.");

  private static NorthboundProblemException CreateNoAdmissibleRoute() =>
      new(
          statusCode: 422,
          problemCode: NoAdmissibleRouteException.ProblemCode,
          title: "Нет допустимого маршрута",
          detail: "Между валидными конечными точками отсутствует допустимый маршрут в текущей топологии.");

  private static NorthboundProblemException CreateJobNotFound() =>
      new(
          statusCode: 404,
          problemCode: "JOB_NOT_FOUND",
          title: "Задание не найдено",
          detail: "Задание с указанным идентификатором отсутствует.");

  private static NorthboundProblemException CreateCancelNotAllowed() =>
      new(
          statusCode: 409,
          problemCode: "CANCEL_NOT_ALLOWED",
          title: "Отмена недопустима",
          detail: "Задание уже находится в терминальном состоянии, в котором отмена не разрешена.");

  private static string NormalizeRequired(string? value, string paramName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
    }

    return value.Trim();
  }
}
