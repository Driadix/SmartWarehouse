using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class PlatformCoreDbContextModelTests
{
  [Fact]
  public void PlatformCoreDbContextMapsAllRequiredSchemas()
  {
    using var context = CreateContext();

    var schemas = context.Model
        .GetEntityTypes()
        .Select(entityType => entityType.GetSchema())
        .OfType<string>()
        .Distinct()
        .OrderBy(value => value)
        .ToArray();

    Assert.Equal(PersistenceSchemas.All.OrderBy(value => value), schemas);
  }

  [Theory]
  [InlineData("topology_versions", PersistenceSchemas.Config)]
  [InlineData("topology_levels", PersistenceSchemas.Config)]
  [InlineData("topology_shafts", PersistenceSchemas.Config)]
  [InlineData("topology_shaft_stops", PersistenceSchemas.Config)]
  [InlineData("jobs", PersistenceSchemas.Wes)]
  [InlineData("execution_task_runtime", PersistenceSchemas.Wcs)]
  [InlineData("outbox_messages", PersistenceSchemas.Integration)]
  [InlineData("payload_transfer_jobs", PersistenceSchemas.Projection)]
  [InlineData("platform_event_journal", PersistenceSchemas.Audit)]
  public void PlatformCoreDbContextMapsRepresentativeTablesToExpectedSchemas(string tableName, string schema)
  {
    using var context = CreateContext();

    Assert.Contains(
        context.Model.GetEntityTypes(),
        entityType => entityType.GetTableName() == tableName && entityType.GetSchema() == schema);
  }

  [Fact]
  public void ConfigSchemaPreservesCanonicalTopologyFields()
  {
    using var context = CreateContext();

    var topologyVersion = AssertEntity<TopologyVersionRecord>(context);
    AssertProperty(topologyVersion, nameof(TopologyVersionRecord.TopologyId), maxLength: 128);
    AssertProperty(topologyVersion, nameof(TopologyVersionRecord.Version));
    AssertIndex(topologyVersion, isUnique: true, nameof(TopologyVersionRecord.TopologyId), nameof(TopologyVersionRecord.Version));

    var topologyLevel = AssertEntity<TopologyLevelRecord>(context);
    AssertProperty(topologyLevel, nameof(TopologyLevelRecord.LevelId), maxLength: 128);
    AssertProperty(topologyLevel, nameof(TopologyLevelRecord.Ordinal));
    AssertProperty(topologyLevel, nameof(TopologyLevelRecord.Name), maxLength: 256);
    AssertIndex(topologyLevel, isUnique: true, nameof(TopologyLevelRecord.TopologyVersionId), nameof(TopologyLevelRecord.Ordinal));

    var topologyNode = AssertEntity<TopologyNodeRecord>(context);
    AssertProperty(topologyNode, nameof(TopologyNodeRecord.LevelId), maxLength: 128);
    AssertProperty(topologyNode, nameof(TopologyNodeRecord.Tags), columnType: "text[]");
    AssertProperty(topologyNode, nameof(TopologyNodeRecord.StationId), maxLength: 128);
    AssertProperty(topologyNode, nameof(TopologyNodeRecord.ShaftId), maxLength: 128);
    AssertProperty(topologyNode, nameof(TopologyNodeRecord.ServicePointId), maxLength: 128);

    var topologyShaft = AssertEntity<TopologyShaftRecord>(context);
    AssertProperty(topologyShaft, nameof(TopologyShaftRecord.CarrierDeviceId), maxLength: 128);
    AssertProperty(topologyShaft, nameof(TopologyShaftRecord.SlotCount));
    AssertIndex(topologyShaft, isUnique: true, nameof(TopologyShaftRecord.TopologyVersionId), nameof(TopologyShaftRecord.CarrierDeviceId));

    var topologyShaftStop = AssertEntity<TopologyShaftStopRecord>(context);
    AssertProperty(topologyShaftStop, nameof(TopologyShaftStopRecord.LevelId), maxLength: 128);
    AssertProperty(topologyShaftStop, nameof(TopologyShaftStopRecord.CarrierNodeId), maxLength: 128);
    AssertProperty(topologyShaftStop, nameof(TopologyShaftStopRecord.TransferPointId), maxLength: 128);
    AssertIndex(topologyShaftStop, isUnique: true, nameof(TopologyShaftStopRecord.TopologyVersionId), nameof(TopologyShaftStopRecord.CarrierNodeId));
    AssertIndex(topologyShaftStop, isUnique: true, nameof(TopologyShaftStopRecord.TopologyVersionId), nameof(TopologyShaftStopRecord.TransferPointId));

    var servicePoint = AssertEntity<TopologyServicePointRecord>(context);
    AssertProperty(servicePoint, nameof(TopologyServicePointRecord.PassiveSemantics), maxLength: 64);

    var deviceBinding = AssertEntity<DeviceBindingRecord>(context);
    AssertProperty(deviceBinding, nameof(DeviceBindingRecord.InitialNodeId), maxLength: 128);
    AssertProperty(deviceBinding, nameof(DeviceBindingRecord.HomeNodeId), maxLength: 128);
    AssertProperty(deviceBinding, nameof(DeviceBindingRecord.ShaftId), maxLength: 128);

    var endpointMapping = AssertEntity<EndpointMappingRecord>(context);
    AssertProperty(endpointMapping, nameof(EndpointMappingRecord.EndpointKind), maxLength: 64);
    AssertProperty(endpointMapping, nameof(EndpointMappingRecord.StationId), maxLength: 128);
    AssertProperty(endpointMapping, nameof(EndpointMappingRecord.ServicePointId), maxLength: 128);

    var job = AssertEntity<JobRecord>(context);
    AssertProperty(job, nameof(JobRecord.ClientOrderId), maxLength: 128);
    AssertProperty(job, nameof(JobRecord.ReasonCode), maxLength: 128);
    AssertProperty(job, nameof(JobRecord.ReasonMessage), maxLength: 1024);
    AssertProperty(job, nameof(JobRecord.CreatedAt));
    AssertProperty(job, nameof(JobRecord.UpdatedAt));
    AssertProperty(job, nameof(JobRecord.CompletedAt));
    AssertIndex(job, isUnique: true, nameof(JobRecord.ClientOrderId));

    var payloadTransferJobProjection = AssertEntity<PayloadTransferJobProjectionRecord>(context);
    AssertProperty(payloadTransferJobProjection, nameof(PayloadTransferJobProjectionRecord.ClientOrderId), maxLength: 128);
    AssertProperty(payloadTransferJobProjection, nameof(PayloadTransferJobProjectionRecord.PayloadRef), columnType: "jsonb");
    AssertProperty(payloadTransferJobProjection, nameof(PayloadTransferJobProjectionRecord.Attributes), columnType: "jsonb");
    AssertProperty(payloadTransferJobProjection, nameof(PayloadTransferJobProjectionRecord.ReasonCode), maxLength: 128);
    AssertProperty(payloadTransferJobProjection, nameof(PayloadTransferJobProjectionRecord.ReasonMessage), maxLength: 1024);
    AssertProperty(payloadTransferJobProjection, nameof(PayloadTransferJobProjectionRecord.CreatedAt));
    AssertProperty(payloadTransferJobProjection, nameof(PayloadTransferJobProjectionRecord.CompletedAt));
    AssertIndex(payloadTransferJobProjection, isUnique: true, nameof(PayloadTransferJobProjectionRecord.ClientOrderId));

    var executionTaskRuntime = AssertEntity<ExecutionTaskRuntimeRecord>(context);
    AssertProperty(executionTaskRuntime, nameof(ExecutionTaskRuntimeRecord.ParticipantRefs), columnType: "jsonb");
    AssertProperty(executionTaskRuntime, nameof(ExecutionTaskRuntimeRecord.ReasonCode), maxLength: 128);
  }

  [Fact]
  public void WcsOperationalTablesPreserveRuntimeSpecificFields()
  {
    using var context = CreateContext();

    var reservation = AssertEntity<ReservationRecord>(context);
    AssertProperty(reservation, nameof(ReservationRecord.ReservedNodeIds), columnType: "text[]");
    AssertIndex(reservation, isUnique: false, nameof(ReservationRecord.OwnerType), nameof(ReservationRecord.OwnerId));

    var deviceSession = AssertEntity<DeviceSessionRecord>(context);
    AssertProperty(deviceSession, nameof(DeviceSessionRecord.State), maxLength: 64);
    AssertIndex(deviceSession, isUnique: true, nameof(DeviceSessionRecord.DeviceId));

    var deviceShadow = AssertEntity<DeviceShadowRecord>(context);
    AssertProperty(deviceShadow, nameof(DeviceShadowRecord.StaticCapabilities), columnType: "text[]");
    AssertProperty(deviceShadow, nameof(DeviceShadowRecord.ActiveCapabilities), columnType: "text[]");
    AssertProperty(deviceShadow, nameof(DeviceShadowRecord.HealthState), maxLength: 64);
    AssertIndex(deviceShadow, isUnique: false, nameof(DeviceShadowRecord.DeviceFamily));
    AssertIndex(deviceShadow, isUnique: false, nameof(DeviceShadowRecord.CurrentNodeId));

    var fault = AssertEntity<FaultRecord>(context);
    AssertProperty(fault, nameof(FaultRecord.FaultCode), maxLength: 128);
    AssertProperty(fault, nameof(FaultRecord.Severity), maxLength: 64);
    AssertIndex(fault, isUnique: false, nameof(FaultRecord.SourceType), nameof(FaultRecord.SourceId), nameof(FaultRecord.State));

    var stationState = AssertEntity<StationBoundaryStateRecord>(context);
    AssertProperty(stationState, nameof(StationBoundaryStateRecord.AttachedNodeId), maxLength: 128);
    AssertProperty(stationState, nameof(StationBoundaryStateRecord.CurrentPayloadId), maxLength: 128);
    AssertIndex(stationState, isUnique: true, nameof(StationBoundaryStateRecord.AttachedNodeId));
    AssertIndex(stationState, isUnique: false, nameof(StationBoundaryStateRecord.Readiness));
  }

  [Fact]
  public void AddPlatformCorePersistenceRegistersDbContext()
  {
    var services = new ServiceCollection();
    services.AddPlatformCorePersistence("Host=localhost;Port=5432;Database=smartwarehouse;Username=smartwarehouse;Password=smartwarehouse");

    using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
    {
      ValidateOnBuild = true,
      ValidateScopes = true
    });
    using var scope = serviceProvider.CreateScope();

    var context = scope.ServiceProvider.GetRequiredService<PlatformCoreDbContext>();

    Assert.NotNull(context);
  }

  [Fact]
  public void PlatformCoreDbContextExposesInitialMigration()
  {
    using var context = CreateContext();

    Assert.Contains(
        context.Database.GetMigrations(),
        migrationId => migrationId.EndsWith("_InitialPlatformCoreSchema", StringComparison.Ordinal));
  }

  private static PlatformCoreDbContext CreateContext()
  {
    var options = new DbContextOptionsBuilder<PlatformCoreDbContext>()
        .UseNpgsql("Host=localhost;Port=5432;Database=smartwarehouse;Username=smartwarehouse;Password=smartwarehouse")
        .Options;

    return new PlatformCoreDbContext(options);
  }

  private static IEntityType AssertEntity<TEntity>(PlatformCoreDbContext context)
  {
    var entityType = context.Model.FindEntityType(typeof(TEntity));
    return Assert.IsAssignableFrom<IEntityType>(entityType);
  }

  private static void AssertProperty(
      IEntityType entityType,
      string propertyName,
      int? maxLength = null,
      string? columnType = null)
  {
    var property = Assert.IsAssignableFrom<IProperty>(entityType.FindProperty(propertyName));

    if (maxLength is { } expectedMaxLength)
    {
      Assert.Equal(expectedMaxLength, property.GetMaxLength());
    }

    if (columnType is not null)
    {
      Assert.Equal(columnType, property.GetColumnType());
    }
  }

  private static void AssertIndex(IEntityType entityType, bool isUnique, params string[] propertyNames)
  {
    var index = entityType.GetIndexes()
        .SingleOrDefault(candidate =>
            candidate.IsUnique == isUnique &&
            candidate.Properties.Select(static property => property.Name).SequenceEqual(propertyNames));

    Assert.NotNull(index);
  }
}
