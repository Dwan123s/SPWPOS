using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NoraSchedulingConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WorkWindowEnd",
                table: "WorkTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WorkWindowStart",
                table: "WorkTasks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OverridesWeeklyAvailability",
                table: "AvailabilityExceptions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkWindowEnd",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "WorkWindowStart",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "OverridesWeeklyAvailability",
                table: "AvailabilityExceptions");
        }
    }
}
