using Microsoft.EntityFrameworkCore;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

public sealed class PayloadTransferJobProjectionRecord
{
  public string JobId { get; set; } = null!;

  public string? ClientOrderId { get; set; }

  public JobType JobType { get; set; }

  public string? PayloadId { get; set; }

  public string SourceEndpointId { get; set; } = null!;

  public string TargetEndpointId { get; set; } = null!;

  public JobState State { get; set; }

  public JobPriority Priority { get; set; }

  public string? LastExecutionTaskId { get; set; }

  public DateTimeOffset LastUpdatedAt { get; set; }
}

public sealed class DigitalTwinDeviceProjectionRecord
{
  public string DeviceId { get; set; } = null!;

  public DeviceFamily DeviceFamily { get; set; }

  public string? CurrentNodeId { get; set; }

  public string HealthState { get; set; } = null!;

  public DeviceExecutionState ExecutionState { get; set; }

  public string[] ActiveCapabilities { get; set; } = [];

  public DateTimeOffset LastUpdatedAt { get; set; }
}

public sealed class DigitalTwinPayloadProjectionRecord
{
  public string PayloadId { get; set; } = null!;

  public string PayloadKind { get; set; } = null!;

  public decimal Length { get; set; }

  public decimal Width { get; set; }

  public decimal Height { get; set; }

  public decimal Weight { get; set; }

  public string CustodyHolderType { get; set; } = null!;

  public string CustodyHolderId { get; set; } = null!;

  public DateTimeOffset LastUpdatedAt { get; set; }
}

public sealed class DigitalTwinStationProjectionRecord
{
  public string StationId { get; set; } = null!;

  public StationType StationType { get; set; }

  public string AttachedNodeId { get; set; } = null!;

  public StationReadiness Readiness { get; set; }

  public string? CurrentPayloadId { get; set; }

  public DateTimeOffset LastUpdatedAt { get; set; }
}

public sealed class DigitalTwinReservationProjectionRecord
{
  public string ReservationId { get; set; } = null!;

  public string OwnerType { get; set; } = null!;

  public string OwnerId { get; set; } = null!;

  public string[] ReservedNodeIds { get; set; } = [];

  public ReservationHorizon Horizon { get; set; }

  public string State { get; set; } = null!;

  public DateTimeOffset LastUpdatedAt { get; set; }
}

internal static class ProjectionSchemaModel
{
  public static void Configure(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<PayloadTransferJobProjectionRecord>(builder =>
    {
      builder.ToTable("payload_transfer_jobs", PersistenceSchemas.Projection);
      builder.HasKey(x => x.JobId);

      builder.Property(x => x.JobId).HasMaxLength(128);
      builder.Property(x => x.ClientOrderId).HasMaxLength(128);
      builder.Property(x => x.JobType).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.PayloadId).HasMaxLength(128);
      builder.Property(x => x.SourceEndpointId).HasMaxLength(128);
      builder.Property(x => x.TargetEndpointId).HasMaxLength(128);
      builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.LastExecutionTaskId).HasMaxLength(128);

      builder.HasIndex(x => x.ClientOrderId).IsUnique();
      builder.HasIndex(x => new { x.State, x.Priority });
    });

    modelBuilder.Entity<DigitalTwinDeviceProjectionRecord>(builder =>
    {
      builder.ToTable("digital_twin_devices", PersistenceSchemas.Projection);
      builder.HasKey(x => x.DeviceId);

      builder.Property(x => x.DeviceId).HasMaxLength(128);
      builder.Property(x => x.DeviceFamily).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.CurrentNodeId).HasMaxLength(128);
      builder.Property(x => x.HealthState).HasMaxLength(64);
      builder.Property(x => x.ExecutionState).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.ActiveCapabilities).HasColumnType("text[]");

      builder.HasIndex(x => x.CurrentNodeId);
    });

    modelBuilder.Entity<DigitalTwinPayloadProjectionRecord>(builder =>
    {
      builder.ToTable("digital_twin_payloads", PersistenceSchemas.Projection);
      builder.HasKey(x => x.PayloadId);

      builder.Property(x => x.PayloadId).HasMaxLength(128);
      builder.Property(x => x.PayloadKind).HasMaxLength(64);
      builder.Property(x => x.Length).HasPrecision(18, 3);
      builder.Property(x => x.Width).HasPrecision(18, 3);
      builder.Property(x => x.Height).HasPrecision(18, 3);
      builder.Property(x => x.Weight).HasPrecision(18, 3);
      builder.Property(x => x.CustodyHolderType).HasMaxLength(32);
      builder.Property(x => x.CustodyHolderId).HasMaxLength(128);

      builder.HasIndex(x => new { x.CustodyHolderType, x.CustodyHolderId });
    });

    modelBuilder.Entity<DigitalTwinStationProjectionRecord>(builder =>
    {
      builder.ToTable("digital_twin_stations", PersistenceSchemas.Projection);
      builder.HasKey(x => x.StationId);

      builder.Property(x => x.StationId).HasMaxLength(128);
      builder.Property(x => x.StationType).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.AttachedNodeId).HasMaxLength(128);
      builder.Property(x => x.Readiness).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.CurrentPayloadId).HasMaxLength(128);

      builder.HasIndex(x => x.AttachedNodeId);
    });

    modelBuilder.Entity<DigitalTwinReservationProjectionRecord>(builder =>
    {
      builder.ToTable("digital_twin_reservations", PersistenceSchemas.Projection);
      builder.HasKey(x => x.ReservationId);

      builder.Property(x => x.ReservationId).HasMaxLength(128);
      builder.Property(x => x.OwnerType).HasMaxLength(32);
      builder.Property(x => x.OwnerId).HasMaxLength(128);
      builder.Property(x => x.ReservedNodeIds).HasColumnType("text[]");
      builder.Property(x => x.Horizon).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.State).HasMaxLength(64);

      builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
    });
  }
}
