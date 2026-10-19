using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve a WORK category already configured by an administrator,
            // including its label and deliberately disabled status.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [task_types] WHERE [Code] = N'WORK')
                    INSERT INTO [task_types] ([Code], [DisplayName], [IsActive])
                    VALUES (N'WORK', N'Work', CAST(1 AS bit));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Category data may predate this migration or be referenced by tasks
            // and estimation history. Rolling back must not delete those records.
        }
    }
}
