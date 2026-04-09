using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Jobs;

public sealed class Job
{
  public Job(
      JobId jobId,
      JobType jobType,
      EndpointId sourceEndpoint,
      EndpointId targetEndpoint,
      JobState state,
      JobPriority priority,
      PayloadId? payloadId = null,
      PlannedRoute? plannedRoute = null,
      IEnumerable<ExecutionTask>? executionTasks = null)
  {
    if (sourceEndpoint == targetEndpoint)
    {
      throw new ArgumentException("Source and target endpoints must be different.", nameof(targetEndpoint));
    }

    var validatedExecutionTasks = DomainGuard.ReadOnlyList(
        executionTasks ?? Array.Empty<ExecutionTask>(),
        nameof(executionTasks));

    var knownTaskIds = new HashSet<ExecutionTaskId>();
    foreach (var executionTask in validatedExecutionTasks)
    {
      if (executionTask.JobId != jobId)
      {
        throw new ArgumentException(
            "Execution task must reference the same job identifier as the aggregate.",
            nameof(executionTasks));
      }

      if (!knownTaskIds.Add(executionTask.TaskId))
      {
        throw new ArgumentException(
            "Execution tasks cannot contain duplicate task identifiers.",
            nameof(executionTasks));
      }
    }

    JobId = jobId;
    JobType = jobType;
    PayloadId = payloadId;
    SourceEndpoint = sourceEndpoint;
    TargetEndpoint = targetEndpoint;
    State = state;
    Priority = priority;
    PlannedRoute = plannedRoute;
    ExecutionTasks = validatedExecutionTasks;
  }

  public JobId JobId { get; }

  public JobType JobType { get; }

  public PayloadId? PayloadId { get; }

  public EndpointId SourceEndpoint { get; }

  public EndpointId TargetEndpoint { get; }

  public JobState State { get; }

  public JobPriority Priority { get; }

  public PlannedRoute? PlannedRoute { get; }

  public IReadOnlyList<ExecutionTask> ExecutionTasks { get; }
}
