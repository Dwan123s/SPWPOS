using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    LatencyMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => x.Id);
                    table.UniqueConstraint("AK_AiUsageRecords_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_AiUsageRecords_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_UserId",
                table: "AiUsageRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_UserId_ReservedAt",
                table: "AiUsageRecords",
                columns: new[] { "UserId", "ReservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_UserId_Status_LeaseUntil",
                table: "AiUsageRecords",
                columns: new[] { "UserId", "Status", "LeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageRecords");
        }
    }
}
