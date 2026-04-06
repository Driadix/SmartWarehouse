using Microsoft.EntityFrameworkCore;
using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

public sealed class TopologyVersionRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string TopologyId { get; set; } = null!;

  public int Version { get; set; }

  public string SourceHash { get; set; } = null!;

  public bool IsActive { get; set; }

  public DateTimeOffset ActivatedAt { get; set; }
}

public sealed class TopologyLevelRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string LevelId { get; set; } = null!;

  public int Ordinal { get; set; }

  public string? Name { get; set; }
}

public sealed class TopologyNodeRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string NodeId { get; set; } = null!;

  public NodeType NodeType { get; set; }

  public string? LevelId { get; set; }

  public string[] Tags { get; set; } = [];

  public string? StationId { get; set; }

  public string? ShaftId { get; set; }

  public string? ServicePointId { get; set; }
}

public sealed class TopologyEdgeRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string EdgeId { get; set; } = null!;

  public string FromNodeId { get; set; } = null!;

  public string ToNodeId { get; set; } = null!;

  public EdgeTraversalMode TraversalMode { get; set; }

  public decimal Weight { get; set; }
}

public sealed class TopologyShaftRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string ShaftId { get; set; } = null!;

  public string CarrierDeviceId { get; set; } = null!;

  public int SlotCount { get; set; }
}

public sealed class TopologyShaftStopRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string ShaftId { get; set; } = null!;

  public string LevelId { get; set; } = null!;

  public string CarrierNodeId { get; set; } = null!;

  public string TransferPointId { get; set; } = null!;
}

public sealed class TopologyStationRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string StationId { get; set; } = null!;

  public StationType StationType { get; set; }

  public string AttachedNodeId { get; set; } = null!;

  public StationControlMode ControlMode { get; set; }

  public int BufferCapacity { get; set; }
}

public sealed class TopologyServicePointRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string ServicePointId { get; set; } = null!;

  public string NodeId { get; set; } = null!;

  public ServicePointType ServicePointType { get; set; }

  public ServicePointPassiveSemantics PassiveSemantics { get; set; }
}

public sealed class DeviceBindingRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string DeviceId { get; set; } = null!;

  public DeviceFamily DeviceFamily { get; set; }

  public string? InitialNodeId { get; set; }

  public string? HomeNodeId { get; set; }

  public string? ShaftId { get; set; }
}

public sealed class EndpointMappingRecord
{
  public string TopologyVersionId { get; set; } = null!;

  public string EndpointId { get; set; } = null!;

  public EndpointKind EndpointKind { get; set; }

  public string? StationId { get; set; }

  public string? ServicePointId { get; set; }
}

internal static class ConfigSchemaModel
{
  public static void Configure(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<TopologyVersionRecord>(builder =>
    {
      builder.ToTable("topology_versions", PersistenceSchemas.Config);
      builder.HasKey(x => x.TopologyVersionId);

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.TopologyId).HasMaxLength(128);
      builder.Property(x => x.SourceHash).HasMaxLength(256);

      builder.HasIndex(x => new { x.TopologyId, x.Version }).IsUnique();
    });

    modelBuilder.Entity<TopologyLevelRecord>(builder =>
    {
      builder.ToTable("topology_levels", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.LevelId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.LevelId).HasMaxLength(128);
      builder.Property(x => x.Name).HasMaxLength(256);

      builder.HasIndex(x => new { x.TopologyVersionId, x.Ordinal }).IsUnique();
    });

    modelBuilder.Entity<TopologyNodeRecord>(builder =>
    {
      builder.ToTable("topology_nodes", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.NodeId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.NodeId).HasMaxLength(128);
      builder.Property(x => x.NodeType).HasConversion<string>().HasMaxLength(64);
      builder.Property(x => x.LevelId).HasMaxLength(128);
      builder.Property(x => x.Tags).HasColumnType("text[]");
      builder.Property(x => x.StationId).HasMaxLength(128);
      builder.Property(x => x.ShaftId).HasMaxLength(128);
      builder.Property(x => x.ServicePointId).HasMaxLength(128);

      builder.HasIndex(x => new { x.TopologyVersionId, x.NodeType });
      builder.HasIndex(x => new { x.TopologyVersionId, x.LevelId });
    });

    modelBuilder.Entity<TopologyEdgeRecord>(builder =>
    {
      builder.ToTable("topology_edges", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.EdgeId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.EdgeId).HasMaxLength(128);
      builder.Property(x => x.FromNodeId).HasMaxLength(128);
      builder.Property(x => x.ToNodeId).HasMaxLength(128);
      builder.Property(x => x.TraversalMode).HasConversion<string>().HasMaxLength(64);
      builder.Property(x => x.Weight).HasPrecision(18, 3);

      builder.HasIndex(x => new { x.TopologyVersionId, x.FromNodeId });
      builder.HasIndex(x => new { x.TopologyVersionId, x.ToNodeId });
    });

    modelBuilder.Entity<TopologyShaftRecord>(builder =>
    {
      builder.ToTable("topology_shafts", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.ShaftId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.ShaftId).HasMaxLength(128);
      builder.Property(x => x.CarrierDeviceId).HasMaxLength(128);

      builder.HasIndex(x => new { x.TopologyVersionId, x.CarrierDeviceId }).IsUnique();
    });

    modelBuilder.Entity<TopologyShaftStopRecord>(builder =>
    {
      builder.ToTable("topology_shaft_stops", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.ShaftId, x.LevelId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.ShaftId).HasMaxLength(128);
      builder.Property(x => x.LevelId).HasMaxLength(128);
      builder.Property(x => x.CarrierNodeId).HasMaxLength(128);
      builder.Property(x => x.TransferPointId).HasMaxLength(128);

      builder.HasIndex(x => new { x.TopologyVersionId, x.CarrierNodeId }).IsUnique();
      builder.HasIndex(x => new { x.TopologyVersionId, x.TransferPointId }).IsUnique();
    });

    modelBuilder.Entity<TopologyStationRecord>(builder =>
    {
      builder.ToTable("topology_stations", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.StationId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.StationId).HasMaxLength(128);
      builder.Property(x => x.StationType).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.AttachedNodeId).HasMaxLength(128);
      builder.Property(x => x.ControlMode).HasConversion<string>().HasMaxLength(32);

      builder.HasIndex(x => new { x.TopologyVersionId, x.AttachedNodeId });
    });

    modelBuilder.Entity<TopologyServicePointRecord>(builder =>
    {
      builder.ToTable("topology_service_points", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.ServicePointId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.ServicePointId).HasMaxLength(128);
      builder.Property(x => x.NodeId).HasMaxLength(128);
      builder.Property(x => x.ServicePointType).HasConversion<string>().HasMaxLength(64);
      builder.Property(x => x.PassiveSemantics).HasConversion<string>().HasMaxLength(64);

      builder.HasIndex(x => new { x.TopologyVersionId, x.NodeId }).IsUnique();
    });

    modelBuilder.Entity<DeviceBindingRecord>(builder =>
    {
      builder.ToTable("device_bindings", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.DeviceId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.DeviceId).HasMaxLength(128);
      builder.Property(x => x.DeviceFamily).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.InitialNodeId).HasMaxLength(128);
      builder.Property(x => x.HomeNodeId).HasMaxLength(128);
      builder.Property(x => x.ShaftId).HasMaxLength(128);
    });

    modelBuilder.Entity<EndpointMappingRecord>(builder =>
    {
      builder.ToTable("endpoint_mappings", PersistenceSchemas.Config);
      builder.HasKey(x => new { x.TopologyVersionId, x.EndpointId });

      builder.Property(x => x.TopologyVersionId).HasMaxLength(128);
      builder.Property(x => x.EndpointId).HasMaxLength(128);
      builder.Property(x => x.EndpointKind).HasConversion<string>().HasMaxLength(64);
      builder.Property(x => x.StationId).HasMaxLength(128);
      builder.Property(x => x.ServicePointId).HasMaxLength(128);
    });
  }
}
