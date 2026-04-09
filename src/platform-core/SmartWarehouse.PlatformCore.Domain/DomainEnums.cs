namespace SmartWarehouse.PlatformCore.Domain;

public enum JobType
{
  PayloadTransfer,
  Charge,
  Relocation
}

public enum JobPriority
{
  Low,
  Normal,
  High
}

public enum JobState
{
  Created,
  Accepted,
  Planned,
  InProgress,
  Suspended,
  Completed,
  Failed,
  Cancelled
}

public enum ExecutionTaskType
{
  Navigate,
  StationTransfer,
  CarrierTransfer
}

public enum ExecutionTaskState
{
  Created,
  Planned,
  InProgress,
  Suspended,
  Completed,
  Failed,
  Cancelled
}

public enum ExecutionResolutionHint
{
  WaitAndRetry,
  ReplanRequired,
  OperatorAttention
}

public enum DeviceFamily
{
  Shuttle3D,
  HybridLift
}

public enum DeviceExecutionState
{
  Idle,
  Executing,
  Suspended,
  Faulted
}

public enum TransferMode
{
  ShuttleRidesHybridLiftWithPayload
}

public enum ShuttleMovementMode
{
  Autonomous,
  CarrierPassenger
}

public enum DispatchStatus
{
  Available,
  Occupied,
  Suspended,
  Maintenance
}

public enum CarrierKind
{
  HybridLift
}

public enum StationType
{
  Load,
  Unload
}

public enum StationControlMode
{
  Passive,
  Active
}

public enum StationReadiness
{
  Ready,
  Blocked,
  Offline,
  Maintenance
}

public enum NodeType
{
  TravelNode,
  SwitchNode,
  TransferPoint,
  CarrierNode,
  StationNode,
  ChargeNode,
  ServiceNode
}

public enum EdgeTraversalMode
{
  Open,
  CarrierOnly,
  Restricted
}

public enum ReservationHorizon
{
  Plan,
  Execution
}

public enum FaultState
{
  Active,
  Cleared
}

public enum ExecutionActorType
{
  Device,
  StationBoundary
}

public enum PayloadHolderType
{
  Device,
  StationBoundary
}

public enum ReservationOwnerType
{
  Job,
  ExecutionTask
}

public enum FaultSourceType
{
  Device,
  StationBoundary
}
