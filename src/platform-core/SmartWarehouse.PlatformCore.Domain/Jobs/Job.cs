using SmartWarehouse.PlatformCore.Domain;
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
      PlannedRoute? plannedRoute = null)
  {
    if (sourceEndpoint == targetEndpoint)
    {
      throw new ArgumentException("Source and target endpoints must be different.", nameof(targetEndpoint));
    }

    JobId = jobId;
    JobType = jobType;
    PayloadId = payloadId;
    SourceEndpoint = sourceEndpoint;
    TargetEndpoint = targetEndpoint;
    State = state;
    Priority = priority;
    PlannedRoute = plannedRoute;
  }

  public JobId JobId { get; }

  public JobType JobType { get; }

  public PayloadId? PayloadId { get; }

  public EndpointId SourceEndpoint { get; }

  public EndpointId TargetEndpoint { get; }

  public JobState State { get; }

  public JobPriority Priority { get; }

  public PlannedRoute? PlannedRoute { get; }
}
