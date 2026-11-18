using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamAvailabilityChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvailabilityCheckCanTake",
                table: "TeamTasks",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AvailabilityCheckUserId",
                table: "TeamTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AvailabilityCheckVersion",
                table: "TeamTasks",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailabilityCheckCanTake",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "AvailabilityCheckUserId",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "AvailabilityCheckVersion",
                table: "TeamTasks");
        }
    }
}
