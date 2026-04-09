using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandWcsExecutionTaskRuntimeStateModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParticipantRefs",
                schema: "wcs",
                table: "execution_task_runtime",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "wcs",
                table: "execution_task_runtime",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReplanRequired",
                schema: "wcs",
                table: "execution_task_runtime",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionHint",
                schema: "wcs",
                table: "execution_task_runtime",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParticipantRefs",
                schema: "wcs",
                table: "execution_task_runtime");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "wcs",
                table: "execution_task_runtime");

            migrationBuilder.DropColumn(
                name: "ReplanRequired",
                schema: "wcs",
                table: "execution_task_runtime");

            migrationBuilder.DropColumn(
                name: "ResolutionHint",
                schema: "wcs",
                table: "execution_task_runtime");
        }
    }
}
