using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentConversationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputFingerprint",
                table: "AgentTurns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AgentConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentConversations", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_AgentConversations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTurns_UserId_ConversationId_CreatedAt_Id",
                table: "AgentTurns",
                columns: new[] { "UserId", "ConversationId", "CreatedAt", "Id" });

            migrationBuilder.Sql("""
                WITH history AS (
                    SELECT UserId, ConversationId, Text, CreatedAt,
                        MIN(CreatedAt) OVER (PARTITION BY UserId, ConversationId) AS FirstAt,
                        MAX(UpdatedAt) OVER (PARTITION BY UserId, ConversationId) AS LastAt,
                        ROW_NUMBER() OVER (PARTITION BY UserId, ConversationId ORDER BY CreatedAt, Id) AS Position
                    FROM AgentTurns
                )
                INSERT INTO AgentConversations (UserId, Id, Title, CreatedAt, UpdatedAt, Version)
                SELECT UserId, ConversationId, CASE WHEN LEN(LTRIM(RTRIM(Text)))=0 THEN N'Hội thoại' ELSE LEFT(Text,120) END,
                    FirstAt, LastAt, NEWID()
                FROM history WHERE Position=1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AgentConversations_UserId_DeletedAt_ArchivedAt_UpdatedAt_Id",
                table: "AgentConversations",
                columns: new[] { "UserId", "DeletedAt", "ArchivedAt", "UpdatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentConversations");

            migrationBuilder.DropIndex(
                name: "IX_AgentTurns_UserId_ConversationId_CreatedAt_Id",
                table: "AgentTurns");

            migrationBuilder.DropColumn(
                name: "InputFingerprint",
                table: "AgentTurns");
        }
    }
}
