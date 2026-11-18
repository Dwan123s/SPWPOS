using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountPremiumPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamPlanPurchases_UserId",
                table: "TeamPlanPurchases");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeamId",
                table: "TeamPlanPurchases",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlanPurchases_UserId_CreatedAt",
                table: "TeamPlanPurchases",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamPlanPurchases_UserId_CreatedAt",
                table: "TeamPlanPurchases");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeamId",
                table: "TeamPlanPurchases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlanPurchases_UserId",
                table: "TeamPlanPurchases",
                column: "UserId");
        }
    }
}
