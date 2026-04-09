using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWarehouse.PlatformCore.Application.Northbound;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wes;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Jobs;
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
    IPayloadTransferJobPlanner planner) : IPayloadTransferJobService
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

      var existingProjection = await FindProjectionByJobIdAsync(existingRegistration.JobId, cancellationToken);
      return new CreatePayloadTransferJobResult(MapToModel(existingProjection), isIdempotentReplay: true);
    }

    if (command.SourceEndpointId == command.TargetEndpointId)
    {
      throw CreateIdenticalEndpoints();
    }

    EnsureEndpointExists(command.SourceEndpointId, isSource: true);
    EnsureEndpointExists(command.TargetEndpointId, isSource: false);

    Job plannedJob;
    try
    {
      plannedJob = planner.Plan(
          new SmartWarehouse.PlatformCore.Domain.Primitives.JobId($"job-{Guid.NewGuid():N}"),
          command.SourceEndpointId,
          command.TargetEndpointId,
          command.Priority);
    }
    catch (NoAdmissibleRouteException)
    {
      throw CreateNoAdmissibleRoute();
    }

    var now = DateTimeOffset.UtcNow;
    var jobRecord = new JobRecord
    {
      JobId = plannedJob.JobId.Value,
      ClientOrderId = command.ClientOrderId,
      JobType = plannedJob.JobType,
      PayloadId = null,
      SourceEndpointId = plannedJob.SourceEndpoint.Value,
      TargetEndpointId = plannedJob.TargetEndpoint.Value,
      State = plannedJob.State,
      Priority = plannedJob.Priority,
      PayloadRef = SerializeOptionalJson(command.PayloadRef),
      Attributes = SerializeOptionalJson(command.Attributes),
      CreatedAt = now,
      UpdatedAt = now
    };
    var projectionRecord = CreateProjectionRecord(jobRecord);

    dbContext.Jobs.Add(jobRecord);
    dbContext.PayloadTransferJobs.Add(projectionRecord);
    dbContext.JobRouteSegments.AddRange(CreateRouteSegmentRecords(plannedJob));
    dbContext.ExecutionTaskPlans.AddRange(CreateExecutionTaskPlanRecords(plannedJob));
    dbContext.ResourceAssignments.AddRange(CreateResourceAssignmentRecords(plannedJob));
    dbContext.NorthboundIdempotency.Add(new NorthboundIdempotencyRecord
    {
      ClientOrderId = command.ClientOrderId,
      RequestHash = requestHash,
      JobId = jobRecord.JobId,
      RegisteredAt = now
    });

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CreatePayloadTransferJobResult(MapToModel(projectionRecord), isIdempotentReplay: false);
  }

  public async Task<PayloadTransferJobModel> GetByJobIdAsync(
      string jobId,
      CancellationToken cancellationToken = default)
  {
    var projectionRecord = await FindProjectionByJobIdAsync(jobId, cancellationToken);
    return MapToModel(projectionRecord);
  }

  public async Task<PayloadTransferJobModel> GetByClientOrderIdAsync(
      string clientOrderId,
      CancellationToken cancellationToken = default)
  {
    var normalizedClientOrderId = NormalizeRequired(clientOrderId, nameof(clientOrderId));
    var projectionRecord = await dbContext.PayloadTransferJobs
        .AsNoTracking()
        .SingleOrDefaultAsync(record => record.ClientOrderId == normalizedClientOrderId, cancellationToken);

    return projectionRecord is not null
        ? MapToModel(projectionRecord)
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

    var projectionRecord = await dbContext.PayloadTransferJobs
        .SingleOrDefaultAsync(record => record.JobId == normalizedJobId, cancellationToken);
    if (projectionRecord is null)
    {
      projectionRecord = CreateProjectionRecord(jobRecord);
      dbContext.PayloadTransferJobs.Add(projectionRecord);
    }

    if (jobRecord.State == JobState.Cancelled)
    {
      ApplyProjectionState(jobRecord, projectionRecord);
      await dbContext.SaveChangesAsync(cancellationToken);
      return new CancelPayloadTransferJobResult(MapToModel(projectionRecord), wasAlreadyCancelled: true);
    }

    if (jobRecord.State is JobState.Completed or JobState.Failed)
    {
      throw CreateCancelNotAllowed();
    }

    var now = DateTimeOffset.UtcNow;
    jobRecord.State = JobState.Cancelled;
    jobRecord.UpdatedAt = now;
    jobRecord.CompletedAt ??= now;
    ApplyProjectionState(jobRecord, projectionRecord);

    await dbContext.SaveChangesAsync(cancellationToken);

    return new CancelPayloadTransferJobResult(MapToModel(projectionRecord), wasAlreadyCancelled: false);
  }

  private void EnsureEndpointExists(SmartWarehouse.PlatformCore.Domain.Primitives.EndpointId endpointId, bool isSource)
  {
    if (topology.TryResolveEndpoint(endpointId, out _))
    {
      return;
    }

    throw isSource ? CreateUnknownSourceEndpoint() : CreateUnknownTargetEndpoint();
  }

  private async Task<PayloadTransferJobProjectionRecord> FindProjectionByJobIdAsync(string jobId, CancellationToken cancellationToken)
  {
    var normalizedJobId = NormalizeRequired(jobId, nameof(jobId));
    var projectionRecord = await dbContext.PayloadTransferJobs
        .AsNoTracking()
        .SingleOrDefaultAsync(record => record.JobId == normalizedJobId, cancellationToken);

    return projectionRecord ?? throw CreateJobNotFound();
  }

  private static PayloadTransferJobModel MapToModel(PayloadTransferJobProjectionRecord record)
  {
    ArgumentNullException.ThrowIfNull(record);

    return new PayloadTransferJobModel(
        record.JobId,
        record.ClientOrderId ?? throw new InvalidOperationException(
            $"Payload transfer job projection '{record.JobId}' does not contain client order identifier."),
        PayloadTransferJobContract.ToExternalState(record.State),
        record.SourceEndpointId,
        record.TargetEndpointId,
        PayloadTransferJobContract.ToExternalPriority(record.Priority),
        record.CreatedAt,
        record.LastUpdatedAt,
        DeserializeOptionalJson(record.PayloadRef),
        DeserializeOptionalJson(record.Attributes),
        record.ReasonCode is null ? null : new PayloadTransferJobReasonModel(record.ReasonCode, record.ReasonMessage),
        record.CompletedAt);
  }

  private static PayloadTransferJobProjectionRecord CreateProjectionRecord(JobRecord jobRecord)
  {
    ArgumentNullException.ThrowIfNull(jobRecord);

    var projectionRecord = new PayloadTransferJobProjectionRecord
    {
      JobId = jobRecord.JobId,
      ClientOrderId = jobRecord.ClientOrderId,
      JobType = jobRecord.JobType,
      PayloadId = jobRecord.PayloadId,
      SourceEndpointId = jobRecord.SourceEndpointId,
      TargetEndpointId = jobRecord.TargetEndpointId,
      PayloadRef = jobRecord.PayloadRef,
      Attributes = jobRecord.Attributes,
      CreatedAt = jobRecord.CreatedAt
    };

    ApplyProjectionState(jobRecord, projectionRecord);
    return projectionRecord;
  }

  private static void ApplyProjectionState(JobRecord jobRecord, PayloadTransferJobProjectionRecord projectionRecord)
  {
    ArgumentNullException.ThrowIfNull(jobRecord);
    ArgumentNullException.ThrowIfNull(projectionRecord);

    projectionRecord.ClientOrderId = jobRecord.ClientOrderId;
    projectionRecord.JobType = jobRecord.JobType;
    projectionRecord.PayloadId = jobRecord.PayloadId;
    projectionRecord.SourceEndpointId = jobRecord.SourceEndpointId;
    projectionRecord.TargetEndpointId = jobRecord.TargetEndpointId;
    projectionRecord.State = jobRecord.State;
    projectionRecord.Priority = jobRecord.Priority;
    projectionRecord.PayloadRef = jobRecord.PayloadRef;
    projectionRecord.Attributes = jobRecord.Attributes;
    projectionRecord.ReasonCode = jobRecord.ReasonCode;
    projectionRecord.ReasonMessage = jobRecord.ReasonMessage;
    projectionRecord.LastUpdatedAt = jobRecord.UpdatedAt;
    projectionRecord.CompletedAt = jobRecord.CompletedAt;
  }

  private static IEnumerable<JobRouteSegmentRecord> CreateRouteSegmentRecords(Job job)
  {
    if (job.PlannedRoute is null)
    {
      yield break;
    }

    for (var index = 0; index < job.PlannedRoute.NodePath.Count; index++)
    {
      yield return new JobRouteSegmentRecord
      {
        JobId = job.JobId.Value,
        SequenceNo = index + 1,
        NodeId = job.PlannedRoute.NodePath[index].Value
      };
    }
  }

  private static IEnumerable<ExecutionTaskPlanRecord> CreateExecutionTaskPlanRecords(Job job)
  {
    for (var index = 0; index < job.ExecutionTasks.Count; index++)
    {
      var executionTask = job.ExecutionTasks[index];
      yield return new ExecutionTaskPlanRecord
      {
        ExecutionTaskId = executionTask.TaskId.Value,
        JobId = job.JobId.Value,
        TaskRevision = index + 1,
        TaskType = executionTask.TaskType,
        State = executionTask.State,
        AssigneeType = executionTask.Assignee.Type.ToString(),
        AssigneeId = executionTask.Assignee.ResourceId,
        SourceNodeId = executionTask.SourceNode?.Value,
        TargetNodeId = executionTask.TargetNode?.Value,
        TransferMode = executionTask.TransferMode,
        CorrelationId = executionTask.CorrelationId.Value
      };
    }
  }

  private static IEnumerable<ResourceAssignmentRecord> CreateResourceAssignmentRecords(Job job)
  {
    foreach (var executionTask in job.ExecutionTasks)
    {
      for (var index = 0; index < executionTask.ParticipantRefs.Count; index++)
      {
        var participantRef = executionTask.ParticipantRefs[index];
        yield return new ResourceAssignmentRecord
        {
          ExecutionTaskId = executionTask.TaskId.Value,
          SequenceNo = index + 1,
          AssignmentRole = CreateAssignmentRole(index + 1),
          ResourceType = participantRef.Type.ToString(),
          ResourceId = participantRef.ResourceId
        };
      }
    }
  }

  private static string CreateAssignmentRole(int sequenceNo) => $"PARTICIPANT_{sequenceNo:D2}";

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
