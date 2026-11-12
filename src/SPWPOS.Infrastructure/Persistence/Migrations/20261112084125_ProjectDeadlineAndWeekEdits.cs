using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectDeadlineAndWeekEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InheritsProjectDeadline",
                table: "WorkTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsWeekEdit",
                table: "AvailabilityExceptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE child
                SET InheritsProjectDeadline = 1
                FROM WorkTasks child
                INNER JOIN WorkTasks project ON project.Id = child.ParentId AND project.UserId = child.UserId
                WHERE child.IsDeleted = 0 AND project.IsDeleted = 0
                    AND child.CreationSource = N'NATURAL_LANGUAGE' AND project.CreationSource = N'NATURAL_LANGUAGE'
                    AND child.CreatedAt = project.CreatedAt AND project.EstimatedMinutes = 0
                    AND child.IsHardDeadline = project.IsHardDeadline
                    AND (child.Deadline = project.Deadline OR (child.Deadline IS NULL AND project.Deadline IS NULL));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InheritsProjectDeadline",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "IsWeekEdit",
                table: "AvailabilityExceptions");
        }
    }
}
