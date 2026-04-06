using Microsoft.EntityFrameworkCore;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

public sealed class PlatformEventJournalRecord
{
  public string EventId { get; set; } = null!;

  public string EventName { get; set; } = null!;

  public ApplicationContractVersion EventVersion { get; set; }

  public DateTimeOffset OccurredAt { get; set; }

  public string CorrelationId { get; set; } = null!;

  public string? CausationId { get; set; }

  public PlatformEventVisibility Visibility { get; set; }

  public string Payload { get; set; } = null!;
}

internal static class AuditSchemaModel
{
  public static void Configure(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<PlatformEventJournalRecord>(builder =>
    {
      builder.ToTable("platform_event_journal", PersistenceSchemas.Audit);
      builder.HasKey(x => x.EventId);

      builder.Property(x => x.EventId).HasMaxLength(128);
      builder.Property(x => x.EventName).HasMaxLength(128);
      builder.Property(x => x.EventVersion)
          .HasConversion(
              value => value.Value,
              value => new ApplicationContractVersion(value))
          .HasMaxLength(32);
      builder.Property(x => x.CorrelationId).HasMaxLength(128);
      builder.Property(x => x.CausationId).HasMaxLength(128);
      builder.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(32);
      builder.Property(x => x.Payload).HasColumnType("jsonb");

      builder.HasIndex(x => x.OccurredAt);
      builder.HasIndex(x => x.CorrelationId);
      builder.HasIndex(x => new { x.EventName, x.OccurredAt });
    });
  }
}
