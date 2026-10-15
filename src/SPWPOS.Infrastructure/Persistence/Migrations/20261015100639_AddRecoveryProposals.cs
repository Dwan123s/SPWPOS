using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecoveryCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TaskIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryCases", x => x.Id);
                    table.UniqueConstraint("AK_RecoveryCases_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_RecoveryCases_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecoveryCases_PlanProposals_UserId_PlanId",
                        columns: x => new { x.UserId, x.PlanId },
                        principalTable: "PlanProposals",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecoverySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    AutoApply = table.Column<bool>(type: "bit", nullable: false),
                    MissedDays = table.Column<int>(type: "int", nullable: false),
                    QuietStartMinute = table.Column<int>(type: "int", nullable: false),
                    QuietEndMinute = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoverySettings", x => x.Id);
                    table.UniqueConstraint("AK_RecoverySettings_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_RecoverySettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryCases_UserId",
                table: "RecoveryCases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryCases_UserId_CreatedAt",
                table: "RecoveryCases",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryCases_UserId_DedupeKey",
                table: "RecoveryCases",
                columns: new[] { "UserId", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryCases_UserId_PlanId",
                table: "RecoveryCases",
                columns: new[] { "UserId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoverySettings_UserId",
                table: "RecoverySettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecoveryCases");

            migrationBuilder.DropTable(
                name: "RecoverySettings");
        }
    }
}
