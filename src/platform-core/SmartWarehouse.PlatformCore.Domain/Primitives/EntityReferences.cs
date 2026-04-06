using SmartWarehouse.PlatformCore.Domain;

namespace SmartWarehouse.PlatformCore.Domain.Primitives;

public readonly record struct ExecutionResourceRef
{
  public ExecutionResourceRef(ExecutionActorType type, string resourceId)
  {
    Type = type;
    ResourceId = DomainGuard.NotWhiteSpace(resourceId, nameof(resourceId));
  }

  public ExecutionActorType Type { get; }

  public string ResourceId { get; }

  public static ExecutionResourceRef ForDevice(DeviceId deviceId) =>
      new(ExecutionActorType.Device, deviceId.Value);

  public static ExecutionResourceRef ForStationBoundary(StationId stationId) =>
      new(ExecutionActorType.StationBoundary, stationId.Value);
}

public readonly record struct PayloadCustodyHolder
{
  public PayloadCustodyHolder(PayloadHolderType holderType, string holderId)
  {
    HolderType = holderType;
    HolderId = DomainGuard.NotWhiteSpace(holderId, nameof(holderId));
  }

  public PayloadHolderType HolderType { get; }

  public string HolderId { get; }

  public static PayloadCustodyHolder ForDevice(DeviceId deviceId) =>
      new(PayloadHolderType.Device, deviceId.Value);

  public static PayloadCustodyHolder ForStationBoundary(StationId stationId) =>
      new(PayloadHolderType.StationBoundary, stationId.Value);
}

public readonly record struct ReservationOwnerRef
{
  public ReservationOwnerRef(ReservationOwnerType ownerType, string ownerId)
  {
    OwnerType = ownerType;
    OwnerId = DomainGuard.NotWhiteSpace(ownerId, nameof(ownerId));
  }

  public ReservationOwnerType OwnerType { get; }

  public string OwnerId { get; }

  public static ReservationOwnerRef ForJob(JobId jobId) =>
      new(ReservationOwnerType.Job, jobId.Value);

  public static ReservationOwnerRef ForExecutionTask(ExecutionTaskId executionTaskId) =>
      new(ReservationOwnerType.ExecutionTask, executionTaskId.Value);
}

public readonly record struct FaultSourceRef
{
  public FaultSourceRef(FaultSourceType sourceType, string sourceId)
  {
    SourceType = sourceType;
    SourceId = DomainGuard.NotWhiteSpace(sourceId, nameof(sourceId));
  }

  public FaultSourceType SourceType { get; }

  public string SourceId { get; }

  public static FaultSourceRef ForDevice(DeviceId deviceId) =>
      new(FaultSourceType.Device, deviceId.Value);

  public static FaultSourceRef ForStationBoundary(StationId stationId) =>
      new(FaultSourceType.StationBoundary, stationId.Value);
}
