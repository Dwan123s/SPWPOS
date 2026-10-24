using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "WorkTasks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Every existing task must be immediately editable with its own
            // nonempty token after upgrading, including completed/deleted tasks.
            migrationBuilder.Sql("UPDATE [WorkTasks] SET [Version] = NEWID();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "WorkTasks");
        }
    }
}
