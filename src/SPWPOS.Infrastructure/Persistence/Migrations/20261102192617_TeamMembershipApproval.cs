using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamMembershipApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "TeamMemberships",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.Sql("UPDATE TeamMemberships SET ApprovalStatus = 'Left' WHERE Active = 0;");

            migrationBuilder.AddColumn<Guid>(
                name: "JoinInviteId",
                table: "TeamMemberships",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedAt",
                table: "TeamMemberships",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_TeamId_ApprovalStatus",
                table: "TeamMemberships",
                columns: new[] { "TeamId", "ApprovalStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamMemberships_TeamId_ApprovalStatus",
                table: "TeamMemberships");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "TeamMemberships");

            migrationBuilder.DropColumn(
                name: "JoinInviteId",
                table: "TeamMemberships");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "TeamMemberships");
        }
    }
}
