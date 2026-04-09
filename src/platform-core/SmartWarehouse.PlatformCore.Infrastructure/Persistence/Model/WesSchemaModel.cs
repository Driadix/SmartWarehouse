using Microsoft.EntityFrameworkCore;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

public sealed class JobRecord
{
  public string JobId { get; set; } = null!;

  public string ClientOrderId { get; set; } = null!;

  public JobType JobType { get; set; }

  public string? PayloadId { get; set; }

  public string SourceEndpointId { get; set; } = null!;

  public string TargetEndpointId { get; set; } = null!;

  public JobState State { get; set; }

  public JobPriority Priority { get; set; }

  public string? PayloadRef { get; set; }

  public string? Attributes { get; set; }

  public string? ReasonCode { get; set; }

  public string? ReasonMessage { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ExecutionTaskPlanRecord
{
  public string ExecutionTaskId { get; set; } = null!;

  public string JobId { get; set; } = null!;

  public int TaskRevision { get; set; }

  public ExecutionTaskType TaskType { get; set; }

  public ExecutionTaskState State { get; set; }

  public string AssigneeType { get; set; } = null!;

  public string AssigneeId { get; set; } = null!;

  public string? SourceNodeId { get; set; }

  public string? TargetNodeId { get; set; }

  public TransferMode? TransferMode { get; set; }

  public string CorrelationId { get; set; } = null!;
}

public sealed class JobRouteSegmentRecord
{
  public string JobId { get; set; } = null!;

  public int SequenceNo { get; set; }

  public string NodeId { get; set; } = null!;
}

public sealed class ResourceAssignmentRecord
{
  public string ExecutionTaskId { get; set; } = null!;

  public int SequenceNo { get; set; }

  public string AssignmentRole { get; set; } = null!;

  public string ResourceType { get; set; } = null!;

  public string ResourceId { get; set; } = null!;
}

internal static class WesSchemaModel
{
  public static void Configure(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<JobRecord>(builder =>
    {
      builder.ToTable("jobs", PersistenceSchemas.Wes);
      builder.HasKey(x => x.JobId);

      builder.Property(x => x.JobId).HasMaxLength(128);
      builder.Property(x => x.ClientOrderId).HasMaxLength(128);
      builder.Property(x => x.JobType).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.PayloadId).HasMaxLength(128);
      builder.Property(x => x.SourceEndpointId).HasMaxLength(128);
      builder.Property(x => x.TargetEndpointId).HasMaxLength(128);
      builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.ReasonCode).HasMaxLength(128);
      builder.Property(x => x.ReasonMessage).HasMaxLength(1024);

      builder.HasIndex(x => x.ClientOrderId).IsUnique();
      builder.HasIndex(x => x.PayloadId);
      builder.HasIndex(x => new { x.State, x.Priority });
    });

    modelBuilder.Entity<ExecutionTaskPlanRecord>(builder =>
    {
      builder.ToTable("execution_task_plans", PersistenceSchemas.Wes);
      builder.HasKey(x => x.ExecutionTaskId);

      builder.Property(x => x.ExecutionTaskId).HasMaxLength(128);
      builder.Property(x => x.JobId).HasMaxLength(128);
      builder.Property(x => x.TaskType).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.AssigneeType).HasMaxLength(32);
      builder.Property(x => x.AssigneeId).HasMaxLength(128);
      builder.Property(x => x.SourceNodeId).HasMaxLength(128);
      builder.Property(x => x.TargetNodeId).HasMaxLength(128);
      builder.Property(x => x.TransferMode).HasConversion<string>().HasMaxLength(64);
      builder.Property(x => x.CorrelationId).HasMaxLength(128);

      builder.HasIndex(x => x.JobId);
      builder.HasIndex(x => new { x.JobId, x.TaskRevision }).IsUnique();
      builder.HasIndex(x => x.CorrelationId).IsUnique();
    });

    modelBuilder.Entity<JobRouteSegmentRecord>(builder =>
    {
      builder.ToTable("job_route_segments", PersistenceSchemas.Wes);
      builder.HasKey(x => new { x.JobId, x.SequenceNo });

      builder.Property(x => x.JobId).HasMaxLength(128);
      builder.Property(x => x.NodeId).HasMaxLength(128);

      builder.HasIndex(x => new { x.JobId, x.NodeId });
    });

    modelBuilder.Entity<ResourceAssignmentRecord>(builder =>
    {
      builder.ToTable("resource_assignments", PersistenceSchemas.Wes);
      builder.HasKey(x => new { x.ExecutionTaskId, x.SequenceNo });

      builder.Property(x => x.ExecutionTaskId).HasMaxLength(128);
      builder.Property(x => x.AssignmentRole).HasMaxLength(32);
      builder.Property(x => x.ResourceType).HasMaxLength(32);
      builder.Property(x => x.ResourceId).HasMaxLength(128);

      builder.HasIndex(x => new { x.ExecutionTaskId, x.AssignmentRole });
    });
  }
}
