using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class AddPosDisplayNameAndPurchaseUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pos_display_name",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "purchase_unit",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE tenants
                SET pos_display_name = name
                WHERE COALESCE(pos_display_name, '') = '';

                UPDATE tenants
                SET name = 'Cone Mart',
                    pos_display_name = 'Cone Mart'
                WHERE name = 'Copenhagen Mart';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pos_display_name",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "purchase_unit",
                table: "products");
        }
    }
}
