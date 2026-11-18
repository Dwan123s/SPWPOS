using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MonthlyPremiumValidity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidUntil",
                table: "TeamPlanPurchases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql("UPDATE [TeamPlanPurchases] SET [ValidUntil] = DATEADD(month, 1, [PaidAt]) WHERE [PaidAt] IS NOT NULL AND [ValidUntil] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "TeamPlanPurchases");
        }
    }
}
