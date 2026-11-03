using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamTaskSourceDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "TeamTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamTaskDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTaskDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamTaskDocuments_AspNetUsers_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamTaskDocuments_TeamProjects_TeamId",
                        column: x => x.TeamId,
                        principalTable: "TeamProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamTasks_SourceDocumentId",
                table: "TeamTasks",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTaskDocuments_TeamId",
                table: "TeamTaskDocuments",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTaskDocuments_UploadedBy",
                table: "TeamTaskDocuments",
                column: "UploadedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamTasks_TeamTaskDocuments_SourceDocumentId",
                table: "TeamTasks",
                column: "SourceDocumentId",
                principalTable: "TeamTaskDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamTasks_TeamTaskDocuments_SourceDocumentId",
                table: "TeamTasks");

            migrationBuilder.DropTable(
                name: "TeamTaskDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TeamTasks_SourceDocumentId",
                table: "TeamTasks");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "TeamTasks");
        }
    }
}
