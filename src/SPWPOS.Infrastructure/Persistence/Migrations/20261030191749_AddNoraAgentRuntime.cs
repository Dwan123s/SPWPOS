using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoraAgentRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotRevision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelTurns = table.Column<int>(type: "int", nullable: false),
                    ToolCalls = table.Column<int>(type: "int", nullable: false),
                    Simulations = table.Column<int>(type: "int", nullable: false),
                    Continuations = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.UniqueConstraint("AK_AgentRuns_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_AgentRuns_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentToolCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Tool = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ArgumentsHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentToolCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentToolCalls_AgentRuns_UserId_RunId",
                        columns: x => new { x.UserId, x.RunId },
                        principalTable: "AgentRuns",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_UserId_ConversationId_UpdatedAt",
                table: "AgentRuns",
                columns: new[] { "UserId", "ConversationId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_UserId_TurnId",
                table: "AgentRuns",
                columns: new[] { "UserId", "TurnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolCalls_RunId_Sequence",
                table: "AgentToolCalls",
                columns: new[] { "RunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolCalls_UserId_RunId",
                table: "AgentToolCalls",
                columns: new[] { "UserId", "RunId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentToolCalls");

            migrationBuilder.DropTable(
                name: "AgentRuns");
        }
    }
}
