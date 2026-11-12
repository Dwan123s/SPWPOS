using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectSourceFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectSourceDocuments",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSourceDocuments", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectSourceDocuments_WorkTasks_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectSourceDocuments");
        }
    }
}
