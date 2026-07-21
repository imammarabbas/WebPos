using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class AddMilkQualityMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "fat_percent",
                table: "daily_milk_collections",
                type: "numeric(12,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "snf_percent",
                table: "daily_milk_collections",
                type: "numeric(12,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fat_percent",
                table: "daily_milk_collections");

            migrationBuilder.DropColumn(
                name: "snf_percent",
                table: "daily_milk_collections");
        }
    }
}
