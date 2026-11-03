using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EisenhowerPrioritization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ImportanceConfidence",
                table: "WorkTasks",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ImportanceConfirmedAt",
                table: "WorkTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportanceLevel",
                table: "WorkTasks",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "ImportanceReason",
                table: "WorkTasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImportanceSource",
                table: "WorkTasks",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<double>(
                name: "ImportanceConfidence",
                table: "TeamTasks",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ImportanceConfirmedAt",
                table: "TeamTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportanceLevel",
                table: "TeamTasks",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "ImportanceReason",
                table: "TeamTasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImportanceSource",
                table: "TeamTasks",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.CreateTable(
                name: "PriorityAlertStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSignal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Episode = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClearSince = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorityAlertStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriorityAlertStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriorityAlertStates_UserId_TaskId",
                table: "PriorityAlertStates",
                columns: new[] { "UserId", "TaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriorityAlertStates");

            migrationBuilder.DropColumn(
                name: "ImportanceConfidence",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceConfirmedAt",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceLevel",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceReason",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceSource",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceConfidence",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceConfirmedAt",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceLevel",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceReason",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "ImportanceSource",
                table: "TeamTasks");
        }
    }
}
