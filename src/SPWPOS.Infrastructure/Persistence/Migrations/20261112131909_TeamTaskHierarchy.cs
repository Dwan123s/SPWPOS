using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamTaskHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentTaskId",
                table: "TeamTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamTasks_ParentTaskId",
                table: "TeamTasks",
                column: "ParentTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamTasks_TeamTasks_ParentTaskId",
                table: "TeamTasks",
                column: "ParentTaskId",
                principalTable: "TeamTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamTasks_TeamTasks_ParentTaskId",
                table: "TeamTasks");

            migrationBuilder.DropIndex(
                name: "IX_TeamTasks_ParentTaskId",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "TeamTasks");
        }
    }
}
