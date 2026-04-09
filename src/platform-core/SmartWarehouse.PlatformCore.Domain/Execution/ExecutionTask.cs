using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Execution;

public sealed class ExecutionTask
{
  public ExecutionTask(
      ExecutionTaskId taskId,
      JobId jobId,
      ExecutionResourceRef assignee,
      IEnumerable<ExecutionResourceRef> participantRefs,
      ExecutionTaskType taskType,
      ExecutionTaskState state,
      CorrelationId correlationId,
      NodeId? sourceNode = null,
      NodeId? targetNode = null,
      TransferMode? transferMode = null)
  {
    var validatedParticipantRefs = DomainGuard.UniqueReadOnlyList(participantRefs, nameof(participantRefs));
    if (validatedParticipantRefs.Contains(assignee))
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

    TaskId = taskId;
    JobId = jobId;
    Assignee = assignee;
    ParticipantRefs = validatedParticipantRefs;
    TaskType = taskType;
    SourceNode = sourceNode;
    TargetNode = targetNode;
    TransferMode = transferMode;
    State = state;
    CorrelationId = correlationId;
  }

  public ExecutionTaskId TaskId { get; }

  public JobId JobId { get; }

  public ExecutionResourceRef Assignee { get; }

  public IReadOnlyList<ExecutionResourceRef> ParticipantRefs { get; }

  public ExecutionTaskType TaskType { get; }

  public NodeId? SourceNode { get; }

  public NodeId? TargetNode { get; }

  public TransferMode? TransferMode { get; }

  public ExecutionTaskState State { get; }

  public CorrelationId CorrelationId { get; }

  public ExecutionTask WithState(ExecutionTaskState state) =>
      new(
          TaskId,
          JobId,
          Assignee,
          ParticipantRefs,
          TaskType,
          state,
          CorrelationId,
          SourceNode,
          TargetNode,
          TransferMode);
}
