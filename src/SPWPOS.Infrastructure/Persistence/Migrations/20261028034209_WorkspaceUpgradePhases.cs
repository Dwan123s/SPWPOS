using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceUpgradePhases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExperienceAwarded",
                table: "WorkSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RepairSourceSessionId",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MutationVersion",
                table: "WorkPreferences",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "ExperiencePoints",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "NoraSkin",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "classic");

            migrationBuilder.AddColumn<int>(
                name: "UserLevel",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_UserId_RepairSourceSessionId",
                table: "WorkSessions",
                columns: new[] { "UserId", "RepairSourceSessionId" },
                unique: true,
                filter: "[RepairSourceSessionId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSessions_WorkSessions_UserId_RepairSourceSessionId",
                table: "WorkSessions",
                columns: new[] { "UserId", "RepairSourceSessionId" },
                principalTable: "WorkSessions",
                principalColumns: new[] { "UserId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkSessions_WorkSessions_UserId_RepairSourceSessionId",
                table: "WorkSessions");

            migrationBuilder.DropIndex(
                name: "IX_WorkSessions_UserId_RepairSourceSessionId",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "ExperienceAwarded",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "RepairSourceSessionId",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "MutationVersion",
                table: "WorkPreferences");

            migrationBuilder.DropColumn(
                name: "ExperiencePoints",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NoraSkin",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserLevel",
                table: "AspNetUsers");
        }
    }
}
