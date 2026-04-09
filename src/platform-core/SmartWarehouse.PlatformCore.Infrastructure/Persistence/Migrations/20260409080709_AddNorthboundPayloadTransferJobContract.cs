using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWarehouse.PlatformCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNorthboundPayloadTransferJobContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Attributes",
                schema: "wes",
                table: "jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientOrderId",
                schema: "wes",
                table: "jobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "wes",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "wes",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "PayloadRef",
                schema: "wes",
                table: "jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "wes",
                table: "jobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonMessage",
                schema: "wes",
                table: "jobs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "wes",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_jobs_ClientOrderId",
                schema: "wes",
                table: "jobs",
                column: "ClientOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_jobs_ClientOrderId",
                schema: "wes",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "Attributes",
                schema: "wes",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "ClientOrderId",
                schema: "wes",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "wes",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "wes",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "PayloadRef",
                schema: "wes",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "wes",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "ReasonMessage",
                schema: "wes",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "wes",
                table: "jobs");
        }
    }
}
