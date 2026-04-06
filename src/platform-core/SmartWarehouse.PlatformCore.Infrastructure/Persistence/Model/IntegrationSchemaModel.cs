using Microsoft.EntityFrameworkCore;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

public sealed class OutboxMessageRecord
{
  public string OutboxId { get; set; } = null!;

  public string Producer { get; set; } = null!;

  public string MessageKind { get; set; } = null!;

  public string AggregateType { get; set; } = null!;

  public string AggregateId { get; set; } = null!;

  public string CorrelationId { get; set; } = null!;

  public string? CausationId { get; set; }

  public string Payload { get; set; } = null!;

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class InboxMessageRecord
{
  public string InboxId { get; set; } = null!;

  public string Source { get; set; } = null!;

  public string MessageId { get; set; } = null!;

  public string CorrelationId { get; set; } = null!;

  public DateTimeOffset ReceivedAt { get; set; }

  public string PayloadHash { get; set; } = null!;

  public DateTimeOffset? HandledAt { get; set; }
}

public sealed class NorthboundIdempotencyRecord
{
  public string ClientOrderId { get; set; } = null!;

  public string RequestHash { get; set; } = null!;

  public string JobId { get; set; } = null!;

  public DateTimeOffset RegisteredAt { get; set; }
}

public sealed class WebhookDeliveryRecord
{
  public string WebhookDeliveryId { get; set; } = null!;

  public string EventId { get; set; } = null!;

  public string EventName { get; set; } = null!;

  public string TargetUrl { get; set; } = null!;

  public string DeliveryState { get; set; } = null!;

  public int AttemptCount { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset? NextAttemptAt { get; set; }

  public DateTimeOffset? LastAttemptAt { get; set; }

  public string? LastError { get; set; }
}

internal static class IntegrationSchemaModel
{
  public static void Configure(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<OutboxMessageRecord>(builder =>
    {
      builder.ToTable("outbox_messages", PersistenceSchemas.Integration);
      builder.HasKey(x => x.OutboxId);

      builder.Property(x => x.OutboxId).HasMaxLength(128);
      builder.Property(x => x.Producer).HasMaxLength(32);
      builder.Property(x => x.MessageKind).HasMaxLength(32);
      builder.Property(x => x.AggregateType).HasMaxLength(128);
      builder.Property(x => x.AggregateId).HasMaxLength(128);
      builder.Property(x => x.CorrelationId).HasMaxLength(128);
      builder.Property(x => x.CausationId).HasMaxLength(128);
      builder.Property(x => x.Payload).HasColumnType("jsonb");

      builder.HasIndex(x => x.PublishedAt);
      builder.HasIndex(x => new { x.Producer, x.MessageKind, x.PublishedAt });
      builder.HasIndex(x => x.CorrelationId);
    });

    modelBuilder.Entity<InboxMessageRecord>(builder =>
    {
      builder.ToTable("inbox_messages", PersistenceSchemas.Integration);
      builder.HasKey(x => x.InboxId);

      builder.Property(x => x.InboxId).HasMaxLength(128);
      builder.Property(x => x.Source).HasMaxLength(128);
      builder.Property(x => x.MessageId).HasMaxLength(128);
      builder.Property(x => x.CorrelationId).HasMaxLength(128);
      builder.Property(x => x.PayloadHash).HasMaxLength(256);

      builder.HasIndex(x => new { x.Source, x.MessageId }).IsUnique();
      builder.HasIndex(x => x.HandledAt);
    });

    modelBuilder.Entity<NorthboundIdempotencyRecord>(builder =>
    {
      builder.ToTable("northbound_idempotency", PersistenceSchemas.Integration);
      builder.HasKey(x => x.ClientOrderId);

      builder.Property(x => x.ClientOrderId).HasMaxLength(128);
      builder.Property(x => x.RequestHash).HasMaxLength(256);
      builder.Property(x => x.JobId).HasMaxLength(128);
    });

    modelBuilder.Entity<WebhookDeliveryRecord>(builder =>
    {
      builder.ToTable("webhook_deliveries", PersistenceSchemas.Integration);
      builder.HasKey(x => x.WebhookDeliveryId);

      builder.Property(x => x.WebhookDeliveryId).HasMaxLength(128);
      builder.Property(x => x.EventId).HasMaxLength(128);
      builder.Property(x => x.EventName).HasMaxLength(128);
      builder.Property(x => x.TargetUrl).HasMaxLength(2048);
      builder.Property(x => x.DeliveryState).HasMaxLength(64);
      builder.Property(x => x.LastError).HasMaxLength(2048);

      builder.HasIndex(x => new { x.EventId, x.TargetUrl });
      builder.HasIndex(x => new { x.DeliveryState, x.NextAttemptAt });
    });
  }
}
