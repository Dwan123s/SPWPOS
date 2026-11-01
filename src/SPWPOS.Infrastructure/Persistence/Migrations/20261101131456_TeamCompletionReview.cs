using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamCompletionReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamChatMessages_TeamId_UserId_RequestId",
                table: "TeamChatMessages");

            migrationBuilder.AddColumn<bool>(
                name: "CompletionApproved",
                table: "TeamTasks",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletionRequestId",
                table: "TeamTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionReviewNote",
                table: "TeamTasks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletionReviewedAt",
                table: "TeamTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletionReviewedBy",
                table: "TeamTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletionSubmittedAt",
                table: "TeamTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressBeforeReview",
                table: "TeamTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "TeamChatMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TeamChatMessages_TeamId_UserId_RequestId_IsSystem",
                table: "TeamChatMessages",
                columns: new[] { "TeamId", "UserId", "RequestId", "IsSystem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamChatMessages_TeamId_UserId_RequestId_IsSystem",
                table: "TeamChatMessages");

            migrationBuilder.DropColumn(
                name: "CompletionApproved",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "CompletionRequestId",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "CompletionReviewNote",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "CompletionReviewedAt",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "CompletionReviewedBy",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "CompletionSubmittedAt",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "ProgressBeforeReview",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "TeamChatMessages");

            migrationBuilder.CreateIndex(
                name: "IX_TeamChatMessages_TeamId_UserId_RequestId",
                table: "TeamChatMessages",
                columns: new[] { "TeamId", "UserId", "RequestId" },
                unique: true);
        }
    }
}
