using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EisenhowerTaskBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChecklistJson",
                table: "WorkTasks",
                type: "nvarchar(max)",
                maxLength: 40000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<long>(
                name: "MatrixOrder",
                table: "WorkTasks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "UrgencyOverride",
                table: "WorkTasks",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Auto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChecklistJson",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "MatrixOrder",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "UrgencyOverride",
                table: "WorkTasks");
        }
    }
}
