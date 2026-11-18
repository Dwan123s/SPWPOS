using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamCompletionPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompletionPolicyVersion",
                table: "TeamProjects",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "NotifyLeaderOnCompletionSubmission",
                table: "TeamProjects",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireCompletionReportUrl",
                table: "TeamProjects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReportUrl",
                table: "TeamCompletionSubmissions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionPolicyVersion",
                table: "TeamProjects");

            migrationBuilder.DropColumn(
                name: "NotifyLeaderOnCompletionSubmission",
                table: "TeamProjects");

            migrationBuilder.DropColumn(
                name: "RequireCompletionReportUrl",
                table: "TeamProjects");

            migrationBuilder.DropColumn(
                name: "ReportUrl",
                table: "TeamCompletionSubmissions");
        }
    }
}
