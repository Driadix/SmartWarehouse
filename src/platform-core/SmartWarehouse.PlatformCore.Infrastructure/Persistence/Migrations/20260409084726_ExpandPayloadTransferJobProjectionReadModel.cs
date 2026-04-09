using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandPayloadTransferJobProjectionReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Attributes",
                schema: "projection",
                table: "payload_transfer_jobs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "projection",
                table: "payload_transfer_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "projection",
                table: "payload_transfer_jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "PayloadRef",
                schema: "projection",
                table: "payload_transfer_jobs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "projection",
                table: "payload_transfer_jobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonMessage",
                schema: "projection",
                table: "payload_transfer_jobs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE projection.payload_transfer_jobs
                SET "CreatedAt" = "LastUpdatedAt"
                WHERE "CreatedAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attributes",
                schema: "projection",
                table: "payload_transfer_jobs");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "projection",
                table: "payload_transfer_jobs");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "projection",
                table: "payload_transfer_jobs");

            migrationBuilder.DropColumn(
                name: "PayloadRef",
                schema: "projection",
                table: "payload_transfer_jobs");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "projection",
                table: "payload_transfer_jobs");

            migrationBuilder.DropColumn(
                name: "ReasonMessage",
                schema: "projection",
                table: "payload_transfer_jobs");
        }
    }
}
