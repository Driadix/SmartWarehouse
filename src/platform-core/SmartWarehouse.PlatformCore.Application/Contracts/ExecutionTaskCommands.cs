using System.Globalization;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Contracts;

public readonly record struct TaskRevision
{
  public TaskRevision(int value) => Value = ContractGuard.Positive(value, nameof(value));

  public int Value { get; }

  public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

  public static implicit operator int(TaskRevision value) => value.Value;
}

public sealed record SubmitExecutionTask : ApplicationCommand
{
  public SubmitExecutionTask(
      ContractEnvelope envelope,
      ExecutionTaskId executionTaskId,
      TaskRevision taskRevision,
      JobId jobId,
      ExecutionTaskType taskType,
      ExecutionResourceRef assigneeRef,
      IEnumerable<ExecutionResourceRef> participantRefs,
      NodeId? sourceNode = null,
      NodeId? targetNode = null,
      TransferMode? transferMode = null)
      : base(nameof(SubmitExecutionTask), envelope)
  {
    var validatedParticipantRefs = ContractGuard.UniqueReadOnlyList(participantRefs, nameof(participantRefs));
    if (validatedParticipantRefs.Contains(assigneeRef))
    {
      throw new ArgumentException(
          "Assignee must not be duplicated in participant references.",
          nameof(participantRefs));
    }

    switch (taskType)
    {
      case ExecutionTaskType.Navigate:
        if (targetNode is null)
        {
          throw new ArgumentException("Navigate task requires a target node.", nameof(targetNode));
        }

        if (transferMode is not null)
        {
          throw new ArgumentException("Navigate task cannot define transfer mode.", nameof(transferMode));
        }

        break;
      case ExecutionTaskType.StationTransfer:
        if (targetNode is null)
        {
          throw new ArgumentException("Station transfer requires a target node.", nameof(targetNode));
        }

        if (validatedParticipantRefs.Count == 0)
        {
          throw new ArgumentException(
              "Station transfer requires at least one participant reference.",
              nameof(participantRefs));
        }

        if (transferMode is not null)
        {
          throw new ArgumentException("Station transfer cannot define transfer mode.", nameof(transferMode));
        }

        break;
      case ExecutionTaskType.CarrierTransfer:
        if (sourceNode is null)
        {
          throw new ArgumentException("Carrier transfer requires a source node.", nameof(sourceNode));
        }

        if (targetNode is null)
        {
          throw new ArgumentException("Carrier transfer requires a target node.", nameof(targetNode));
        }

        if (transferMode is null)
        {
          throw new ArgumentException("Carrier transfer requires transfer mode.", nameof(transferMode));
        }

        if (validatedParticipantRefs.Count == 0)
        {
          throw new ArgumentException(
              "Carrier transfer requires participant references.",
              nameof(participantRefs));
        }

        break;
    }

    ExecutionTaskId = executionTaskId;
    TaskRevision = taskRevision;
    JobId = jobId;
    TaskType = taskType;
    AssigneeRef = assigneeRef;
    ParticipantRefs = validatedParticipantRefs;
    SourceNode = sourceNode;
    TargetNode = targetNode;
    TransferMode = transferMode;
  }

  public ExecutionTaskId ExecutionTaskId { get; }

  public TaskRevision TaskRevision { get; }

  public JobId JobId { get; }

  public ExecutionTaskType TaskType { get; }

  public ExecutionResourceRef AssigneeRef { get; }

  public IReadOnlyList<ExecutionResourceRef> ParticipantRefs { get; }

  public NodeId? SourceNode { get; }

  public NodeId? TargetNode { get; }

  public TransferMode? TransferMode { get; }

  public static SubmitExecutionTask FromExecutionTask(
      EnvelopeId messageId,
      TaskRevision taskRevision,
      ExecutionTask executionTask,
      CausationId? causationId = null,
      ApplicationContractVersion? contractVersion = null) =>
      new(
          new ContractEnvelope(messageId, executionTask.CorrelationId, causationId, contractVersion),
          executionTask.TaskId,
          taskRevision,
          executionTask.JobId,
          executionTask.TaskType,
          executionTask.Assignee,
          executionTask.ParticipantRefs,
          executionTask.SourceNode,
          executionTask.TargetNode,
          executionTask.TransferMode);
}

public sealed record CancelExecutionTask : ApplicationCommand
{
  public CancelExecutionTask(
      ContractEnvelope envelope,
      ExecutionTaskId executionTaskId,
      TaskRevision taskRevision,
      JobId jobId,
      ReasonCode? reasonCode = null)
      : base(nameof(CancelExecutionTask), envelope)
  {
    ExecutionTaskId = executionTaskId;
    TaskRevision = taskRevision;
    JobId = jobId;
    ReasonCode = reasonCode;
  }

  public ExecutionTaskId ExecutionTaskId { get; }

  public TaskRevision TaskRevision { get; }

  public JobId JobId { get; }

  public ReasonCode? ReasonCode { get; }

  public static CancelExecutionTask ForExecutionTask(
      EnvelopeId messageId,
      TaskRevision taskRevision,
      ExecutionTask executionTask,
      ReasonCode? reasonCode = null,
      CausationId? causationId = null,
      ApplicationContractVersion? contractVersion = null) =>
      new(
          new ContractEnvelope(messageId, executionTask.CorrelationId, causationId, contractVersion),
          executionTask.TaskId,
          taskRevision,
          executionTask.JobId,
          reasonCode);
}
