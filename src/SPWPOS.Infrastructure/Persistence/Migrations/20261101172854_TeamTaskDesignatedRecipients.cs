using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamTaskDesignatedRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DesignatedUserId",
                table: "TeamTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamTasks_DesignatedUserId",
                table: "TeamTasks",
                column: "DesignatedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamTasks_AspNetUsers_DesignatedUserId",
                table: "TeamTasks",
                column: "DesignatedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamTasks_AspNetUsers_DesignatedUserId",
                table: "TeamTasks");

            migrationBuilder.DropIndex(
                name: "IX_TeamTasks_DesignatedUserId",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "DesignatedUserId",
                table: "TeamTasks");
        }
    }
}
