using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoReschedule",
                table: "WorkPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumDailyMinutes",
                table: "WorkPreferences",
                type: "int",
                nullable: false,
                defaultValue: 480);

            migrationBuilder.CreateTable(
                name: "AgentTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CommandJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UndoJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedRevision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedRevision = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTurns", x => x.Id);
                    table.UniqueConstraint("AK_AgentTurns_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_AgentTurns_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTurns_UserId",
                table: "AgentTurns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTurns_UserId_CreatedAt",
                table: "AgentTurns",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTurns_UserId_RequestId",
                table: "AgentTurns",
                columns: new[] { "UserId", "RequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentTurns");

            migrationBuilder.DropColumn(
                name: "AutoReschedule",
                table: "WorkPreferences");

            migrationBuilder.DropColumn(
                name: "MaximumDailyMinutes",
                table: "WorkPreferences");
        }
    }
}
