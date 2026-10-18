using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailEnabled",
                table: "notification_settings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EmailAttempts",
                table: "InAppNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EmailPending",
                table: "InAppNotifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailSentAt",
                table: "InAppNotifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                table: "InAppNotifications",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextEmailAttemptAt",
                table: "InAppNotifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InAppNotifications_UserId_EmailPending_NextEmailAttemptAt",
                table: "InAppNotifications",
                columns: new[] { "UserId", "EmailPending", "NextEmailAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InAppNotifications_UserId_EmailPending_NextEmailAttemptAt",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "EmailEnabled",
                table: "notification_settings");

            migrationBuilder.DropColumn(
                name: "EmailAttempts",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "EmailPending",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "IsVisible",
                table: "InAppNotifications");

            migrationBuilder.DropColumn(
                name: "NextEmailAttemptAt",
                table: "InAppNotifications");
        }
    }
}
