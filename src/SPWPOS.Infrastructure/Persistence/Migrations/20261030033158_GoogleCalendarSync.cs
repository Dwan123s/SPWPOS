using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoogleCalendarSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarOAuthRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StateHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReminderMinutes = table.Column<int>(type: "int", nullable: false),
                    RemindAtStart = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOAuthRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarOAuthRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarSyncOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LocalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSyncOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSyncOutbox_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoogleCalendarConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleSubject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ProtectedRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalendarId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SyncToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextSyncAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    ReminderMinutes = table.Column<int>(type: "int", nullable: false),
                    RemindAtStart = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleCalendarConnections", x => x.Id);
                    table.UniqueConstraint("AK_GoogleCalendarConnections_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_GoogleCalendarConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEventLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LocalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleEventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Etag = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BaseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConflictJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    RemoteDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    SeriesId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventLinks_GoogleCalendarConnections_UserId_ConnectionId",
                        columns: x => new { x.UserId, x.ConnectionId },
                        principalTable: "GoogleCalendarConnections",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarWatchChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarWatchChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarWatchChannels_GoogleCalendarConnections_UserId_ConnectionId",
                        columns: x => new { x.UserId, x.ConnectionId },
                        principalTable: "GoogleCalendarConnections",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_ConnectionId_GoogleEventId",
                table: "CalendarEventLinks",
                columns: new[] { "ConnectionId", "GoogleEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_ConnectionId_Kind_LocalId",
                table: "CalendarEventLinks",
                columns: new[] { "ConnectionId", "Kind", "LocalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_UserId_ConnectionId",
                table: "CalendarEventLinks",
                columns: new[] { "UserId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOAuthRequests_ExpiresAt",
                table: "CalendarOAuthRequests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOAuthRequests_StateHash",
                table: "CalendarOAuthRequests",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOAuthRequests_UserId",
                table: "CalendarOAuthRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncOutbox_UserId_CreatedAt",
                table: "CalendarSyncOutbox",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarWatchChannels_ExpiresAt",
                table: "CalendarWatchChannels",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarWatchChannels_UserId_ConnectionId",
                table: "CalendarWatchChannels",
                columns: new[] { "UserId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarConnections_Status_NextSyncAt",
                table: "GoogleCalendarConnections",
                columns: new[] { "Status", "NextSyncAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarConnections_UserId",
                table: "GoogleCalendarConnections",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEventLinks");

            migrationBuilder.DropTable(
                name: "CalendarOAuthRequests");

            migrationBuilder.DropTable(
                name: "CalendarSyncOutbox");

            migrationBuilder.DropTable(
                name: "CalendarWatchChannels");

            migrationBuilder.DropTable(
                name: "GoogleCalendarConnections");
        }
    }
}
