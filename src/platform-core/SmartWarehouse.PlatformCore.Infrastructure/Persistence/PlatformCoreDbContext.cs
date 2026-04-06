using Microsoft.EntityFrameworkCore;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence;

public sealed class PlatformCoreDbContext(DbContextOptions<PlatformCoreDbContext> options) : DbContext(options)
{
  internal DbSet<TopologyVersionRecord> TopologyVersions => Set<TopologyVersionRecord>();
  internal DbSet<TopologyNodeRecord> TopologyNodes => Set<TopologyNodeRecord>();
  internal DbSet<TopologyEdgeRecord> TopologyEdges => Set<TopologyEdgeRecord>();
  internal DbSet<TopologyStationRecord> TopologyStations => Set<TopologyStationRecord>();
  internal DbSet<TopologyServicePointRecord> TopologyServicePoints => Set<TopologyServicePointRecord>();
  internal DbSet<DeviceBindingRecord> DeviceBindings => Set<DeviceBindingRecord>();
  internal DbSet<EndpointMappingRecord> EndpointMappings => Set<EndpointMappingRecord>();

  internal DbSet<JobRecord> Jobs => Set<JobRecord>();
  internal DbSet<ExecutionTaskPlanRecord> ExecutionTaskPlans => Set<ExecutionTaskPlanRecord>();
  internal DbSet<JobRouteSegmentRecord> JobRouteSegments => Set<JobRouteSegmentRecord>();
  internal DbSet<ResourceAssignmentRecord> ResourceAssignments => Set<ResourceAssignmentRecord>();

  internal DbSet<ExecutionTaskRuntimeRecord> ExecutionTaskRuntime => Set<ExecutionTaskRuntimeRecord>();
  internal DbSet<ReservationRecord> Reservations => Set<ReservationRecord>();
  internal DbSet<DeviceSessionRecord> DeviceSessions => Set<DeviceSessionRecord>();
  internal DbSet<DeviceShadowRecord> DeviceShadows => Set<DeviceShadowRecord>();
  internal DbSet<FaultRecord> Faults => Set<FaultRecord>();
  internal DbSet<StationBoundaryStateRecord> StationBoundaryStates => Set<StationBoundaryStateRecord>();

  internal DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();
  internal DbSet<InboxMessageRecord> InboxMessages => Set<InboxMessageRecord>();
  internal DbSet<NorthboundIdempotencyRecord> NorthboundIdempotency => Set<NorthboundIdempotencyRecord>();
  internal DbSet<WebhookDeliveryRecord> WebhookDeliveries => Set<WebhookDeliveryRecord>();

  internal DbSet<PayloadTransferJobProjectionRecord> PayloadTransferJobs => Set<PayloadTransferJobProjectionRecord>();
  internal DbSet<DigitalTwinDeviceProjectionRecord> DigitalTwinDevices => Set<DigitalTwinDeviceProjectionRecord>();
  internal DbSet<DigitalTwinPayloadProjectionRecord> DigitalTwinPayloads => Set<DigitalTwinPayloadProjectionRecord>();
  internal DbSet<DigitalTwinStationProjectionRecord> DigitalTwinStations => Set<DigitalTwinStationProjectionRecord>();
  internal DbSet<DigitalTwinReservationProjectionRecord> DigitalTwinReservations => Set<DigitalTwinReservationProjectionRecord>();

  internal DbSet<PlatformEventJournalRecord> PlatformEventJournal => Set<PlatformEventJournalRecord>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    ConfigSchemaModel.Configure(modelBuilder);
    WesSchemaModel.Configure(modelBuilder);
    WcsSchemaModel.Configure(modelBuilder);
    IntegrationSchemaModel.Configure(modelBuilder);
    ProjectionSchemaModel.Configure(modelBuilder);
    AuditSchemaModel.Configure(modelBuilder);
  }
}
