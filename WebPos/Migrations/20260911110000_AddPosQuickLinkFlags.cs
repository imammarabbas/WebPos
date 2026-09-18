using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class AddPosQuickLinkFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "show_on_pos_quick",
                table: "categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "pos_quick_sort",
                table: "categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "show_on_pos_quick",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "pos_quick_sort",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "show_on_pos_quick",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "pos_quick_sort",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "show_on_pos_quick",
                table: "products");

            migrationBuilder.DropColumn(
                name: "pos_quick_sort",
                table: "products");
        }
    }
}
