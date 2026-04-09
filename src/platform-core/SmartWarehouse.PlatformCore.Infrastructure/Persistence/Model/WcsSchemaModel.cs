using Microsoft.EntityFrameworkCore;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

public sealed class ExecutionTaskRuntimeRecord
{
  public string ExecutionTaskId { get; set; } = null!;

  public string JobId { get; set; } = null!;

  public int TaskRevision { get; set; }

  public ExecutionTaskType TaskType { get; set; }

  public ExecutionTaskState State { get; set; }

  public string AssigneeType { get; set; } = null!;

  public string AssigneeId { get; set; } = null!;

  public string ParticipantRefs { get; set; } = "[]";

  public string? SourceNodeId { get; set; }

  public string? TargetNodeId { get; set; }

  public TransferMode? TransferMode { get; set; }

  public string CorrelationId { get; set; } = null!;

  public string? ActiveRuntimePhase { get; set; }

  public string? ReasonCode { get; set; }

  public ExecutionResolutionHint? ResolutionHint { get; set; }

  public bool? ReplanRequired { get; set; }
}

public sealed class ReservationRecord
{
  public string ReservationId { get; set; } = null!;

  public string OwnerType { get; set; } = null!;

  public string OwnerId { get; set; } = null!;

  public string[] ReservedNodeIds { get; set; } = [];

  public ReservationHorizon Horizon { get; set; }

  public string State { get; set; } = null!;
}

public sealed class DeviceSessionRecord
{
  public string DeviceSessionId { get; set; } = null!;

  public string DeviceId { get; set; } = null!;

  public string State { get; set; } = null!;

  public DateTimeOffset LeaseUntil { get; set; }

  public DateTimeOffset LastHeartbeatAt { get; set; }
}

public sealed class DeviceShadowRecord
{
  public string DeviceId { get; set; } = null!;

  public DeviceFamily DeviceFamily { get; set; }

  public string? CurrentNodeId { get; set; }

  public string HealthState { get; set; } = null!;

  public DeviceExecutionState ExecutionState { get; set; }

  public string[] StaticCapabilities { get; set; } = [];

  public string[] ActiveCapabilities { get; set; } = [];

  public string? MovementMode { get; set; }

  public DispatchStatus? DispatchStatus { get; set; }

  public string? CarrierId { get; set; }

  public string? CarriedPayloadId { get; set; }

  public CarrierKind? CarrierKind { get; set; }

  public int? SlotCount { get; set; }

  public string? OccupiedShuttleId { get; set; }

  public DateTimeOffset LastObservedAt { get; set; }
}

public sealed class FaultRecord
{
  public string FaultId { get; set; } = null!;

  public string SourceType { get; set; } = null!;

  public string SourceId { get; set; } = null!;

  public string FaultCode { get; set; } = null!;

  public string Severity { get; set; } = null!;

  public FaultState State { get; set; }
}

public sealed class StationBoundaryStateRecord
{
  public string StationId { get; set; } = null!;

  public StationType StationType { get; set; }

  public string AttachedNodeId { get; set; } = null!;

  public StationControlMode ControlMode { get; set; }

  public StationReadiness Readiness { get; set; }

  public int BufferCapacity { get; set; }

  public string? CurrentPayloadId { get; set; }

  public DateTimeOffset LastUpdatedAt { get; set; }
}

internal static class WcsSchemaModel
{
  public static void Configure(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<ExecutionTaskRuntimeRecord>(builder =>
    {
      builder.ToTable("execution_task_runtime", PersistenceSchemas.Wcs);
      builder.HasKey(x => x.ExecutionTaskId);

      builder.Property(x => x.ExecutionTaskId).HasMaxLength(128);
      builder.Property(x => x.JobId).HasMaxLength(128);
      builder.Property(x => x.TaskType).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.AssigneeType).HasMaxLength(32);
      builder.Property(x => x.AssigneeId).HasMaxLength(128);
      builder.Property(x => x.ParticipantRefs).HasColumnType("jsonb");
      builder.Property(x => x.SourceNodeId).HasMaxLength(128);
      builder.Property(x => x.TargetNodeId).HasMaxLength(128);
      builder.Property(x => x.TransferMode).HasConversion<string>().HasMaxLength(64);
      builder.Property(x => x.CorrelationId).HasMaxLength(128);
      builder.Property(x => x.ActiveRuntimePhase).HasMaxLength(64);
      builder.Property(x => x.ReasonCode).HasMaxLength(128);
      builder.Property(x => x.ResolutionHint).HasConversion<string>().HasMaxLength(32);

      builder.HasIndex(x => x.JobId);
      builder.HasIndex(x => x.CorrelationId).IsUnique();
      builder.HasIndex(x => new { x.State, x.AssigneeId });
    });

    modelBuilder.Entity<ReservationRecord>(builder =>
    {
      builder.ToTable("reservations", PersistenceSchemas.Wcs);
      builder.HasKey(x => x.ReservationId);

      builder.Property(x => x.ReservationId).HasMaxLength(128);
      builder.Property(x => x.OwnerType).HasMaxLength(32);
      builder.Property(x => x.OwnerId).HasMaxLength(128);
      builder.Property(x => x.ReservedNodeIds).HasColumnType("text[]");
      builder.Property(x => x.Horizon).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.State).HasMaxLength(64);

      builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
    });

    modelBuilder.Entity<DeviceSessionRecord>(builder =>
    {
      builder.ToTable("device_sessions", PersistenceSchemas.Wcs);
      builder.HasKey(x => x.DeviceSessionId);

      builder.Property(x => x.DeviceSessionId).HasMaxLength(128);
      builder.Property(x => x.DeviceId).HasMaxLength(128);
      builder.Property(x => x.State).HasMaxLength(64);

      builder.HasIndex(x => x.DeviceId).IsUnique();
      builder.HasIndex(x => x.LeaseUntil);
    });

    modelBuilder.Entity<DeviceShadowRecord>(builder =>
    {
      builder.ToTable("device_shadows", PersistenceSchemas.Wcs);
      builder.HasKey(x => x.DeviceId);

      builder.Property(x => x.DeviceId).HasMaxLength(128);
      builder.Property(x => x.DeviceFamily).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.CurrentNodeId).HasMaxLength(128);
      builder.Property(x => x.HealthState).HasMaxLength(64);
      builder.Property(x => x.ExecutionState).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.StaticCapabilities).HasColumnType("text[]");
      builder.Property(x => x.ActiveCapabilities).HasColumnType("text[]");
      builder.Property(x => x.MovementMode).HasMaxLength(64);
      builder.Property(x => x.DispatchStatus).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.CarrierId).HasMaxLength(128);
      builder.Property(x => x.CarriedPayloadId).HasMaxLength(128);
      builder.Property(x => x.CarrierKind).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.OccupiedShuttleId).HasMaxLength(128);

      builder.HasIndex(x => x.DeviceFamily);
      builder.HasIndex(x => x.CurrentNodeId);
    });

    modelBuilder.Entity<FaultRecord>(builder =>
    {
      builder.ToTable("faults", PersistenceSchemas.Wcs);
      builder.HasKey(x => x.FaultId);

      builder.Property(x => x.FaultId).HasMaxLength(128);
      builder.Property(x => x.SourceType).HasMaxLength(32);
      builder.Property(x => x.SourceId).HasMaxLength(128);
      builder.Property(x => x.FaultCode).HasMaxLength(128);
      builder.Property(x => x.Severity).HasMaxLength(64);
      builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);

      builder.HasIndex(x => new { x.SourceType, x.SourceId, x.State });
    });

    modelBuilder.Entity<StationBoundaryStateRecord>(builder =>
    {
      builder.ToTable("station_boundary_state", PersistenceSchemas.Wcs);
      builder.HasKey(x => x.StationId);

      builder.Property(x => x.StationId).HasMaxLength(128);
      builder.Property(x => x.StationType).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.AttachedNodeId).HasMaxLength(128);
      builder.Property(x => x.ControlMode).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.Readiness).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.CurrentPayloadId).HasMaxLength(128);

      builder.HasIndex(x => x.AttachedNodeId).IsUnique();
      builder.HasIndex(x => x.Readiness);
    });
  }
}
