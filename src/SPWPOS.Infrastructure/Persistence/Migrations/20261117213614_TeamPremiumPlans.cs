using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamPremiumPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamPlanPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AmountVnd = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaymentLinkId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ClosedStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceiptReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamPlanPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamPlanPurchases_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamPlanPurchases_TeamProjects_TeamId",
                        column: x => x.TeamId,
                        principalTable: "TeamProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlanPurchases_OrderCode",
                table: "TeamPlanPurchases",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlanPurchases_PaymentLinkId",
                table: "TeamPlanPurchases",
                column: "PaymentLinkId",
                unique: true,
                filter: "[PaymentLinkId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlanPurchases_ReceiptReference",
                table: "TeamPlanPurchases",
                column: "ReceiptReference",
                unique: true,
                filter: "[ReceiptReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlanPurchases_TeamId_CreatedAt",
                table: "TeamPlanPurchases",
                columns: new[] { "TeamId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlanPurchases_UserId",
                table: "TeamPlanPurchases",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamPlanPurchases");

        }
    }
}
