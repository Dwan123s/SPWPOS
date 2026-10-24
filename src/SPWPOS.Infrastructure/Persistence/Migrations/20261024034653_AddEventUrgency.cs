using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPWPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventUrgency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUrgent",
                table: "FixedEvents",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUrgent",
                table: "FixedEvents");
        }
    }
}
