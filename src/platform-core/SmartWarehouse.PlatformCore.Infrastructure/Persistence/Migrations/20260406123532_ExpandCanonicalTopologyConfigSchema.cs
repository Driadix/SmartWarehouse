using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCanonicalTopologyConfigSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_topology_versions_VersionLabel",
                schema: "config",
                table: "topology_versions");

            migrationBuilder.DropColumn(
                name: "VersionLabel",
                schema: "config",
                table: "topology_versions");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "config",
                table: "topology_nodes");

            migrationBuilder.DropColumn(
                name: "AttachedNodeId",
                schema: "config",
                table: "endpoint_mappings");

            migrationBuilder.RenameColumn(
                name: "MappingKind",
                schema: "config",
                table: "endpoint_mappings",
                newName: "EndpointKind");

            migrationBuilder.AddColumn<string>(
                name: "TopologyId",
                schema: "config",
                table: "topology_versions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "config",
                table: "topology_versions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PassiveSemantics",
                schema: "config",
                table: "topology_service_points",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LevelId",
                schema: "config",
                table: "topology_nodes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServicePointId",
                schema: "config",
                table: "topology_nodes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShaftId",
                schema: "config",
                table: "topology_nodes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StationId",
                schema: "config",
                table: "topology_nodes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "Tags",
                schema: "config",
                table: "topology_nodes",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "ServicePointId",
                schema: "config",
                table: "endpoint_mappings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StationId",
                schema: "config",
                table: "endpoint_mappings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HomeNodeId",
                schema: "config",
                table: "device_bindings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<string>(
                name: "InitialNodeId",
                schema: "config",
                table: "device_bindings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShaftId",
                schema: "config",
                table: "device_bindings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "topology_levels",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LevelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_levels", x => new { x.TopologyVersionId, x.LevelId });
                });

            migrationBuilder.CreateTable(
                name: "topology_shaft_stops",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ShaftId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LevelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CarrierNodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TransferPointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_shaft_stops", x => new { x.TopologyVersionId, x.ShaftId, x.LevelId });
                });

            migrationBuilder.CreateTable(
                name: "topology_shafts",
                schema: "config",
                columns: table => new
                {
                    TopologyVersionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ShaftId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CarrierDeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SlotCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_shafts", x => new { x.TopologyVersionId, x.ShaftId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_topology_versions_TopologyId_Version",
                schema: "config",
                table: "topology_versions",
                columns: new[] { "TopologyId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topology_service_points_TopologyVersionId_NodeId",
                schema: "config",
                table: "topology_service_points",
                columns: new[] { "TopologyVersionId", "NodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topology_nodes_TopologyVersionId_LevelId",
                schema: "config",
                table: "topology_nodes",
                columns: new[] { "TopologyVersionId", "LevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_topology_levels_TopologyVersionId_Ordinal",
                schema: "config",
                table: "topology_levels",
                columns: new[] { "TopologyVersionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topology_shaft_stops_TopologyVersionId_CarrierNodeId",
                schema: "config",
                table: "topology_shaft_stops",
                columns: new[] { "TopologyVersionId", "CarrierNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topology_shaft_stops_TopologyVersionId_TransferPointId",
                schema: "config",
                table: "topology_shaft_stops",
                columns: new[] { "TopologyVersionId", "TransferPointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topology_shafts_TopologyVersionId_CarrierDeviceId",
                schema: "config",
                table: "topology_shafts",
                columns: new[] { "TopologyVersionId", "CarrierDeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "topology_levels",
                schema: "config");

            migrationBuilder.DropTable(
                name: "topology_shaft_stops",
                schema: "config");

            migrationBuilder.DropTable(
                name: "topology_shafts",
                schema: "config");

            migrationBuilder.DropIndex(
                name: "IX_topology_versions_TopologyId_Version",
                schema: "config",
                table: "topology_versions");

            migrationBuilder.DropIndex(
                name: "IX_topology_service_points_TopologyVersionId_NodeId",
                schema: "config",
                table: "topology_service_points");

            migrationBuilder.DropIndex(
                name: "IX_topology_nodes_TopologyVersionId_LevelId",
                schema: "config",
                table: "topology_nodes");

            migrationBuilder.DropColumn(
                name: "TopologyId",
                schema: "config",
                table: "topology_versions");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "config",
                table: "topology_versions");

            migrationBuilder.DropColumn(
                name: "PassiveSemantics",
                schema: "config",
                table: "topology_service_points");

            migrationBuilder.DropColumn(
                name: "LevelId",
                schema: "config",
                table: "topology_nodes");

            migrationBuilder.DropColumn(
                name: "ServicePointId",
                schema: "config",
                table: "topology_nodes");

            migrationBuilder.DropColumn(
                name: "ShaftId",
                schema: "config",
                table: "topology_nodes");

            migrationBuilder.DropColumn(
                name: "StationId",
                schema: "config",
                table: "topology_nodes");

            migrationBuilder.DropColumn(
                name: "Tags",
                schema: "config",
                table: "topology_nodes");

            migrationBuilder.DropColumn(
                name: "ServicePointId",
                schema: "config",
                table: "endpoint_mappings");

            migrationBuilder.DropColumn(
                name: "StationId",
                schema: "config",
                table: "endpoint_mappings");

            migrationBuilder.DropColumn(
                name: "InitialNodeId",
                schema: "config",
                table: "device_bindings");

            migrationBuilder.DropColumn(
                name: "ShaftId",
                schema: "config",
                table: "device_bindings");

            migrationBuilder.RenameColumn(
                name: "EndpointKind",
                schema: "config",
                table: "endpoint_mappings",
                newName: "MappingKind");

            migrationBuilder.AddColumn<string>(
                name: "VersionLabel",
                schema: "config",
                table: "topology_versions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                schema: "config",
                table: "topology_nodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachedNodeId",
                schema: "config",
                table: "endpoint_mappings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "HomeNodeId",
                schema: "config",
                table: "device_bindings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_topology_versions_VersionLabel",
                schema: "config",
                table: "topology_versions",
                column: "VersionLabel",
                unique: true);
        }
    }
}
