using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SocialProfilesAndFriendships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "user_profiles",
                type: "nvarchar(max)",
                maxLength: 3000000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    UserLowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserHighId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => new { x.UserLowId, x.UserHighId });
                    table.CheckConstraint("CK_Friendships_Pair", "[UserLowId] <> [UserHighId]");
                    table.CheckConstraint("CK_Friendships_Requester", "[RequestedById] = [UserLowId] OR [RequestedById] = [UserHighId]");
                    table.CheckConstraint("CK_Friendships_Status", "[Status] IN ('Pending', 'Accepted')");
                    table.ForeignKey(
                        name: "FK_Friendships_AspNetUsers_UserHighId",
                        column: x => x.UserHighId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Friendships_AspNetUsers_UserLowId",
                        column: x => x.UserLowId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserHighId_Status",
                table: "Friendships",
                columns: new[] { "UserHighId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserLowId_Status",
                table: "Friendships",
                columns: new[] { "UserLowId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "user_profiles");
        }
    }
}
