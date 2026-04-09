using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartWarehouse.PlatformCore.Infrastructure.Wcs;

public static class PersistenceWcsExecutionTaskCommandProcessingServiceCollectionExtensions
{
  public static IServiceCollection AddPersistenceWcsExecutionTaskCommandProcessing(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddWcsExecutionTaskCommandProcessing<PersistenceWcsExecutionTaskCommandProcessor>();

    return services;
  }
}

internal sealed class PersistenceWcsExecutionTaskCommandProcessor(PlatformCoreDbContext dbContext) : IWcsExecutionTaskCommandProcessor
{
  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
  {
    Converters =
    {
      new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
    }
  };

  public async ValueTask SubmitAsync(SubmitExecutionTask command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var existingRecord = await dbContext.ExecutionTaskRuntime
        .SingleOrDefaultAsync(record => record.ExecutionTaskId == command.ExecutionTaskId.Value, cancellationToken);
    var submittedRuntime = CreateSubmittedRuntime(command);

    if (existingRecord is null)
    {
      dbContext.ExecutionTaskRuntime.Add(CreateRecord(submittedRuntime));
      await dbContext.SaveChangesAsync(cancellationToken);
      return;
    }

    var existingRuntime = MapToRuntime(existingRecord);
    if (existingRuntime.TaskRevision == submittedRuntime.TaskRevision)
    {
      EnsureEquivalentPlan(existingRecord, submittedRuntime);
      return;
    }

    if (existingRuntime.TaskRevision > submittedRuntime.TaskRevision)
    {
      throw new InvalidOperationException(
          $"Execution task '{command.ExecutionTaskId}' already has newer revision '{existingRuntime.TaskRevision}'.");
    }

    if (!existingRuntime.IsTerminal)
    {
      throw new InvalidOperationException(
          $"Execution task '{command.ExecutionTaskId}' cannot accept revision '{submittedRuntime.TaskRevision}' while state is '{existingRuntime.Task.State}'.");
    }

    ApplyRecord(existingRecord, submittedRuntime);
    await dbContext.SaveChangesAsync(cancellationToken);
  }

  public async ValueTask CancelAsync(CancelExecutionTask command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var existingRecord = await dbContext.ExecutionTaskRuntime
        .SingleOrDefaultAsync(record => record.ExecutionTaskId == command.ExecutionTaskId.Value, cancellationToken);
    if (existingRecord is null)
    {
      throw new InvalidOperationException($"Execution task runtime '{command.ExecutionTaskId}' was not found.");
    }

    if (existingRecord.TaskRevision > command.TaskRevision.Value)
    {
      return;
    }

    if (existingRecord.TaskRevision != command.TaskRevision.Value)
    {
      throw new InvalidOperationException(
          $"Execution task '{command.ExecutionTaskId}' expects revision '{existingRecord.TaskRevision}', but '{command.TaskRevision}' was requested.");
    }

    var runtime = MapToRuntime(existingRecord);
    if (runtime.Task.State == ExecutionTaskState.Cancelled)
    {
      return;
    }

    if (runtime.IsTerminal)
    {
      throw new InvalidOperationException(
          $"Execution task '{command.ExecutionTaskId}' is already terminal in state '{runtime.Task.State}'.");
    }

    var cancelledRuntime = runtime.Cancel(new RuntimePhase("Cancelled"), command.ReasonCode);
    ApplyRecord(existingRecord, cancelledRuntime);

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  private static ExecutionTaskRuntime CreateSubmittedRuntime(SubmitExecutionTask command) =>
      ExecutionTaskRuntime.CreateSubmitted(
          new ExecutionTask(
              command.ExecutionTaskId,
              command.JobId,
              command.AssigneeRef,
              command.ParticipantRefs,
              command.TaskType,
              ExecutionTaskState.Planned,
              command.Envelope.CorrelationId,
              command.SourceNode,
              command.TargetNode,
              command.TransferMode),
          command.TaskRevision.Value);

  private static ExecutionTaskRuntime MapToRuntime(ExecutionTaskRuntimeRecord record)
  {
    ArgumentNullException.ThrowIfNull(record);

    return new ExecutionTaskRuntime(
        new ExecutionTask(
            new ExecutionTaskId(record.ExecutionTaskId),
            new JobId(record.JobId),
            new ExecutionResourceRef(Enum.Parse<ExecutionActorType>(record.AssigneeType, ignoreCase: false), record.AssigneeId),
            DeserializeParticipantRefs(record.ParticipantRefs),
            record.TaskType,
            record.State,
            new CorrelationId(record.CorrelationId),
            record.SourceNodeId is null ? null : new NodeId(record.SourceNodeId),
            record.TargetNodeId is null ? null : new NodeId(record.TargetNodeId),
            record.TransferMode),
        record.TaskRevision,
        new RuntimePhase(record.ActiveRuntimePhase ?? "Accepted"),
        record.ReasonCode is null ? null : new ReasonCode(record.ReasonCode),
        record.ResolutionHint,
        record.ReplanRequired);
  }

  private static ExecutionTaskRuntimeRecord CreateRecord(ExecutionTaskRuntime runtime) =>
      new()
      {
        ExecutionTaskId = runtime.Task.TaskId.Value,
        JobId = runtime.Task.JobId.Value,
        TaskRevision = runtime.TaskRevision,
        TaskType = runtime.Task.TaskType,
        State = runtime.Task.State,
        AssigneeType = runtime.Task.Assignee.Type.ToString(),
        AssigneeId = runtime.Task.Assignee.ResourceId,
        ParticipantRefs = SerializeParticipantRefs(runtime.Task.ParticipantRefs),
        SourceNodeId = runtime.Task.SourceNode?.Value,
        TargetNodeId = runtime.Task.TargetNode?.Value,
        TransferMode = runtime.Task.TransferMode,
        CorrelationId = runtime.Task.CorrelationId.Value,
        ActiveRuntimePhase = runtime.ActiveRuntimePhase.Value,
        ReasonCode = runtime.ReasonCode?.Value,
        ResolutionHint = runtime.ResolutionHint,
        ReplanRequired = runtime.ReplanRequired
      };

  private static void ApplyRecord(ExecutionTaskRuntimeRecord record, ExecutionTaskRuntime runtime)
  {
    ArgumentNullException.ThrowIfNull(record);
    ArgumentNullException.ThrowIfNull(runtime);

    record.JobId = runtime.Task.JobId.Value;
    record.TaskRevision = runtime.TaskRevision;
    record.TaskType = runtime.Task.TaskType;
    record.State = runtime.Task.State;
    record.AssigneeType = runtime.Task.Assignee.Type.ToString();
    record.AssigneeId = runtime.Task.Assignee.ResourceId;
    record.ParticipantRefs = SerializeParticipantRefs(runtime.Task.ParticipantRefs);
    record.SourceNodeId = runtime.Task.SourceNode?.Value;
    record.TargetNodeId = runtime.Task.TargetNode?.Value;
    record.TransferMode = runtime.Task.TransferMode;
    record.CorrelationId = runtime.Task.CorrelationId.Value;
    record.ActiveRuntimePhase = runtime.ActiveRuntimePhase.Value;
    record.ReasonCode = runtime.ReasonCode?.Value;
    record.ResolutionHint = runtime.ResolutionHint;
    record.ReplanRequired = runtime.ReplanRequired;
  }

  private static void EnsureEquivalentPlan(ExecutionTaskRuntimeRecord existingRecord, ExecutionTaskRuntime submittedRuntime)
  {
    ArgumentNullException.ThrowIfNull(existingRecord);
    ArgumentNullException.ThrowIfNull(submittedRuntime);

    if (existingRecord.JobId != submittedRuntime.Task.JobId.Value ||
        existingRecord.TaskType != submittedRuntime.Task.TaskType ||
        existingRecord.AssigneeType != submittedRuntime.Task.Assignee.Type.ToString() ||
        existingRecord.AssigneeId != submittedRuntime.Task.Assignee.ResourceId ||
        existingRecord.ParticipantRefs != SerializeParticipantRefs(submittedRuntime.Task.ParticipantRefs) ||
        existingRecord.SourceNodeId != submittedRuntime.Task.SourceNode?.Value ||
        existingRecord.TargetNodeId != submittedRuntime.Task.TargetNode?.Value ||
        existingRecord.TransferMode != submittedRuntime.Task.TransferMode ||
        existingRecord.CorrelationId != submittedRuntime.Task.CorrelationId.Value)
    {
      throw new InvalidOperationException(
          $"Execution task '{submittedRuntime.Task.TaskId}' was resubmitted with the same revision but a different immutable plan.");
    }
  }

  private static string SerializeParticipantRefs(IReadOnlyList<ExecutionResourceRef> participantRefs) =>
      JsonSerializer.Serialize(participantRefs, SerializerOptions);

  private static ExecutionResourceRef[] DeserializeParticipantRefs(string payload) =>
      JsonSerializer.Deserialize<ExecutionResourceRef[]>(payload, SerializerOptions) ??
      Array.Empty<ExecutionResourceRef>();
}
