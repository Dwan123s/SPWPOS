using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelegramNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FixedEventId",
                table: "InAppNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextTelegramAttemptAt",
                table: "InAppNotifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TelegramAttempts",
                table: "InAppNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TelegramBindingId",
                table: "InAppNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TelegramPending",
                table: "InAppNotifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramSentAt",
                table: "InAppNotifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TelegramConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    BindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LinkTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LinkExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TelegramUpdateCursors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NextOffset = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramUpdateCursors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InAppNotifications_UserId_TelegramPending_NextTelegramAttemptAt",
                table: "InAppNotifications",
                columns: new[] { "UserId", "TelegramPending", "NextTelegramAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramConnections_ChatId",
                table: "TelegramConnections",
                column: "ChatId",
                unique: true,
                filter: "[ChatId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramConnections_LinkTokenHash",
                table: "TelegramConnections",
                column: "LinkTokenHash",
                unique: true,
                filter: "[LinkTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramConnections_UserId",
                table: "TelegramConnections",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramConnections");

            migrationBuilder.DropTable(
                name: "TelegramUpdateCursors");

            migrationBuilder.DropIndex(
                name: "IX_InAppNotifications_UserId_TelegramPending_NextTelegramAttemptAt",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "FixedEventId",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "NextTelegramAttemptAt",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "TelegramAttempts",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "TelegramBindingId",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "TelegramPending",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "TelegramSentAt",
                table: "InAppNotifications");
        }
    }
}
