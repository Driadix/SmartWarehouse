using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlatformCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "config");

            migrationBuilder.EnsureSchema(
                name: "wcs");

            migrationBuilder.EnsureSchema(
                name: "projection");

            migrationBuilder.EnsureSchema(
                name: "wes");

            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "device_bindings",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceFamily = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HomeNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_bindings", x => new { x.TopologyVersionId, x.DeviceId });
                });

            migrationBuilder.CreateTable(
                name: "device_sessions",
                schema: "wcs",
                columns: table => new
                {
                    DeviceSessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_sessions", x => x.DeviceSessionId);
                });

            migrationBuilder.CreateTable(
                name: "device_shadows",
                schema: "wcs",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceFamily = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    HealthState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExecutionState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StaticCapabilities = table.Column<string[]>(type: "text[]", nullable: false),
                    ActiveCapabilities = table.Column<string[]>(type: "text[]", nullable: false),
                    MovementMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DispatchStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CarrierId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CarriedPayloadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CarrierKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SlotCount = table.Column<int>(type: "integer", nullable: true),
                    OccupiedShuttleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_shadows", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "digital_twin_devices",
                schema: "projection",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceFamily = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    HealthState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExecutionState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActiveCapabilities = table.Column<string[]>(type: "text[]", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_twin_devices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "digital_twin_payloads",
                schema: "projection",
                columns: table => new
                {
                    PayloadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Length = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Width = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Height = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CustodyHolderType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CustodyHolderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_twin_payloads", x => x.PayloadId);
                });

            migrationBuilder.CreateTable(
                name: "digital_twin_reservations",
                schema: "projection",
                columns: table => new
                {
                    ReservationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservedNodeIds = table.Column<string[]>(type: "text[]", nullable: false),
                    Horizon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_twin_reservations", x => x.ReservationId);
                });

            migrationBuilder.CreateTable(
                name: "digital_twin_stations",
                schema: "projection",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttachedNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Readiness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentPayloadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_twin_stations", x => x.StationId);
                });

            migrationBuilder.CreateTable(
                name: "endpoint_mappings",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EndpointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AttachedNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MappingKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endpoint_mappings", x => new { x.TopologyVersionId, x.EndpointId });
                });

            migrationBuilder.CreateTable(
                name: "execution_task_plans",
                schema: "wes",
                columns: table => new
                {
                    ExecutionTaskId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    JobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TaskRevision = table.Column<int>(type: "integer", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssigneeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssigneeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TargetNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TransferMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_task_plans", x => x.ExecutionTaskId);
                });

            migrationBuilder.CreateTable(
                name: "execution_task_runtime",
                schema: "wcs",
                columns: table => new
                {
                    ExecutionTaskId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    JobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TaskRevision = table.Column<int>(type: "integer", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssigneeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssigneeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TargetNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TransferMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActiveRuntimePhase = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_task_runtime", x => x.ExecutionTaskId);
                });

            migrationBuilder.CreateTable(
                name: "faults",
                schema: "wcs",
                columns: table => new
                {
                    FaultId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FaultCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faults", x => x.FaultId);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "integration",
                columns: table => new
                {
                    InboxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HandledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.InboxId);
                });

            migrationBuilder.CreateTable(
                name: "job_route_segments",
                schema: "wes",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_route_segments", x => new { x.JobId, x.SequenceNo });
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "wes",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    JobType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SourceEndpointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetEndpointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "northbound_idempotency",
                schema: "integration",
                columns: table => new
                {
                    ClientOrderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    JobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_northbound_idempotency", x => x.ClientOrderId);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "integration",
                columns: table => new
                {
                    OutboxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Producer = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MessageKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CausationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "payload_transfer_jobs",
                schema: "projection",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientOrderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    JobType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SourceEndpointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetEndpointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastExecutionTaskId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payload_transfer_jobs", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "platform_event_journal",
                schema: "audit",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CausationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_event_journal", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "wcs",
                columns: table => new
                {
                    ReservationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservedNodeIds = table.Column<string[]>(type: "text[]", nullable: false),
                    Horizon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.ReservationId);
                });

            migrationBuilder.CreateTable(
                name: "resource_assignments",
                schema: "wes",
                columns: table => new
                {
                    ExecutionTaskId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    AssignmentRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_assignments", x => new { x.ExecutionTaskId, x.SequenceNo });
                });

            migrationBuilder.CreateTable(
                name: "station_boundary_state",
                schema: "wcs",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttachedNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ControlMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Readiness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BufferCapacity = table.Column<int>(type: "integer", nullable: false),
                    CurrentPayloadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_station_boundary_state", x => x.StationId);
                });

            migrationBuilder.CreateTable(
                name: "topology_edges",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EdgeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FromNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ToNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TraversalMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_edges", x => new { x.TopologyVersionId, x.EdgeId });
                });

            migrationBuilder.CreateTable(
                name: "topology_nodes",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_nodes", x => new { x.TopologyVersionId, x.NodeId });
                });

            migrationBuilder.CreateTable(
                name: "topology_service_points",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ServicePointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ServicePointType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_service_points", x => new { x.TopologyVersionId, x.ServicePointId });
                });

            migrationBuilder.CreateTable(
                name: "topology_stations",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttachedNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ControlMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BufferCapacity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_stations", x => new { x.TopologyVersionId, x.StationId });
                });

            migrationBuilder.CreateTable(
                name: "topology_versions",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_versions", x => x.TopologyVersionId);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                schema: "integration",
                columns: table => new
                {
                    WebhookDeliveryId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    DeliveryState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.WebhookDeliveryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_sessions_DeviceId",
                schema: "wcs",
                table: "device_sessions",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_sessions_LeaseUntil",
                schema: "wcs",
                table: "device_sessions",
                column: "LeaseUntil");

            migrationBuilder.CreateIndex(
                name: "IX_device_shadows_CurrentNodeId",
                schema: "wcs",
                table: "device_shadows",
                column: "CurrentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_device_shadows_DeviceFamily",
                schema: "wcs",
                table: "device_shadows",
                column: "DeviceFamily");

            migrationBuilder.CreateIndex(
                name: "IX_digital_twin_devices_CurrentNodeId",
                schema: "projection",
                table: "digital_twin_devices",
                column: "CurrentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_digital_twin_payloads_CustodyHolderType_CustodyHolderId",
                schema: "projection",
                table: "digital_twin_payloads",
                columns: new[] { "CustodyHolderType", "CustodyHolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_digital_twin_reservations_OwnerType_OwnerId",
                schema: "projection",
                table: "digital_twin_reservations",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_digital_twin_stations_AttachedNodeId",
                schema: "projection",
                table: "digital_twin_stations",
                column: "AttachedNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_task_plans_CorrelationId",
                schema: "wes",
                table: "execution_task_plans",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_task_plans_JobId",
                schema: "wes",
                table: "execution_task_plans",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_task_plans_JobId_TaskRevision",
                schema: "wes",
                table: "execution_task_plans",
                columns: new[] { "JobId", "TaskRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_task_runtime_CorrelationId",
                schema: "wcs",
                table: "execution_task_runtime",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_task_runtime_JobId",
                schema: "wcs",
                table: "execution_task_runtime",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_task_runtime_State_AssigneeId",
                schema: "wcs",
                table: "execution_task_runtime",
                columns: new[] { "State", "AssigneeId" });

            migrationBuilder.CreateIndex(
                name: "IX_faults_SourceType_SourceId_State",
                schema: "wcs",
                table: "faults",
                columns: new[] { "SourceType", "SourceId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_HandledAt",
                schema: "integration",
                table: "inbox_messages",
                column: "HandledAt");

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Source_MessageId",
                schema: "integration",
                table: "inbox_messages",
                columns: new[] { "Source", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_route_segments_JobId_NodeId",
                schema: "wes",
                table: "job_route_segments",
                columns: new[] { "JobId", "NodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_PayloadId",
                schema: "wes",
                table: "jobs",
                column: "PayloadId");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_State_Priority",
                schema: "wes",
                table: "jobs",
                columns: new[] { "State", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_CorrelationId",
                schema: "integration",
                table: "outbox_messages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Producer_MessageKind_PublishedAt",
                schema: "integration",
                table: "outbox_messages",
                columns: new[] { "Producer", "MessageKind", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAt",
                schema: "integration",
                table: "outbox_messages",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_payload_transfer_jobs_ClientOrderId",
                schema: "projection",
                table: "payload_transfer_jobs",
                column: "ClientOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payload_transfer_jobs_State_Priority",
                schema: "projection",
                table: "payload_transfer_jobs",
                columns: new[] { "State", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_event_journal_CorrelationId",
                schema: "audit",
                table: "platform_event_journal",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_event_journal_EventName_OccurredAt",
                schema: "audit",
                table: "platform_event_journal",
                columns: new[] { "EventName", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_event_journal_OccurredAt",
                schema: "audit",
                table: "platform_event_journal",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_OwnerType_OwnerId",
                schema: "wcs",
                table: "reservations",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_resource_assignments_ExecutionTaskId_AssignmentRole",
                schema: "wes",
                table: "resource_assignments",
                columns: new[] { "ExecutionTaskId", "AssignmentRole" });

            migrationBuilder.CreateIndex(
                name: "IX_station_boundary_state_AttachedNodeId",
                schema: "wcs",
                table: "station_boundary_state",
                column: "AttachedNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_station_boundary_state_Readiness",
                schema: "wcs",
                table: "station_boundary_state",
                column: "Readiness");

            migrationBuilder.CreateIndex(
                name: "IX_topology_edges_TopologyVersionId_FromNodeId",
                schema: "config",
                table: "topology_edges",
                columns: new[] { "TopologyVersionId", "FromNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_topology_edges_TopologyVersionId_ToNodeId",
                schema: "config",
                table: "topology_edges",
                columns: new[] { "TopologyVersionId", "ToNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_topology_nodes_TopologyVersionId_NodeType",
                schema: "config",
                table: "topology_nodes",
                columns: new[] { "TopologyVersionId", "NodeType" });

            migrationBuilder.CreateIndex(
                name: "IX_topology_stations_TopologyVersionId_AttachedNodeId",
                schema: "config",
                table: "topology_stations",
                columns: new[] { "TopologyVersionId", "AttachedNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_topology_versions_VersionLabel",
                schema: "config",
                table: "topology_versions",
                column: "VersionLabel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_DeliveryState_NextAttemptAt",
                schema: "integration",
                table: "webhook_deliveries",
                columns: new[] { "DeliveryState", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_EventId_TargetUrl",
                schema: "integration",
                table: "webhook_deliveries",
                columns: new[] { "EventId", "TargetUrl" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_bindings",
                schema: "config");

            migrationBuilder.DropTable(
                name: "device_sessions",
                schema: "wcs");

            migrationBuilder.DropTable(
                name: "device_shadows",
                schema: "wcs");

            migrationBuilder.DropTable(
                name: "digital_twin_devices",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "digital_twin_payloads",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "digital_twin_reservations",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "digital_twin_stations",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "endpoint_mappings",
                schema: "config");

            migrationBuilder.DropTable(
                name: "execution_task_plans",
                schema: "wes");

            migrationBuilder.DropTable(
                name: "execution_task_runtime",
                schema: "wcs");

            migrationBuilder.DropTable(
                name: "faults",
                schema: "wcs");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "job_route_segments",
                schema: "wes");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "wes");

            migrationBuilder.DropTable(
                name: "northbound_idempotency",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "payload_transfer_jobs",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "platform_event_journal",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "wcs");

            migrationBuilder.DropTable(
                name: "resource_assignments",
                schema: "wes");

            migrationBuilder.DropTable(
                name: "station_boundary_state",
                schema: "wcs");

            migrationBuilder.DropTable(
                name: "topology_edges",
                schema: "config");

            migrationBuilder.DropTable(
                name: "topology_nodes",
                schema: "config");

            migrationBuilder.DropTable(
                name: "topology_service_points",
                schema: "config");

            migrationBuilder.DropTable(
                name: "topology_stations",
                schema: "config");

            migrationBuilder.DropTable(
                name: "topology_versions",
                schema: "config");

            migrationBuilder.DropTable(
                name: "webhook_deliveries",
                schema: "integration");
        }
    }
}
