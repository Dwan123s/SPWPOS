using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PetExperienceAndSkinPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TaskExperienceAwarded",
                table: "WorkTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing completed work keeps its original XP; reopening it must
            // not create a new task-completion reward after this upgrade.
            migrationBuilder.Sql("UPDATE WorkTasks SET TaskExperienceAwarded = 1 WHERE Status = 2;");

            migrationBuilder.CreateTable(
                name: "SkinPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    SkinCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AmountVnd = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentLinkId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceiptReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkinPurchases", x => x.Id);
                    table.UniqueConstraint("AK_SkinPurchases_UserId_Id", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_SkinPurchases_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OwnedPetSkins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkinCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnedPetSkins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnedPetSkins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OwnedPetSkins_SkinPurchases_UserId_PurchaseId",
                        columns: x => new { x.UserId, x.PurchaseId },
                        principalTable: "SkinPurchases",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OwnedPetSkins_UserId_PurchaseId",
                table: "OwnedPetSkins",
                columns: new[] { "UserId", "PurchaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_OwnedPetSkins_UserId_SkinCode",
                table: "OwnedPetSkins",
                columns: new[] { "UserId", "SkinCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkinPurchases_OrderCode",
                table: "SkinPurchases",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkinPurchases_PaymentLinkId",
                table: "SkinPurchases",
                column: "PaymentLinkId",
                unique: true,
                filter: "[PaymentLinkId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SkinPurchases_ReceiptReference",
                table: "SkinPurchases",
                column: "ReceiptReference",
                unique: true,
                filter: "[ReceiptReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SkinPurchases_UserId_RequestId",
                table: "SkinPurchases",
                columns: new[] { "UserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkinPurchases_UserId_SkinCode_Status",
                table: "SkinPurchases",
                columns: new[] { "UserId", "SkinCode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OwnedPetSkins");

            migrationBuilder.DropTable(
                name: "SkinPurchases");

            migrationBuilder.DropColumn(
                name: "TaskExperienceAwarded",
                table: "WorkTasks");
        }
    }
}
