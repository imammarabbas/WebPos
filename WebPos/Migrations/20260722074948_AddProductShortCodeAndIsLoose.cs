using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class AddProductShortCodeAndIsLoose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_loose",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "short_code",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id_short_code",
                table: "products",
                columns: new[] { "tenant_id", "short_code" },
                unique: true,
                filter: "short_code <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_tenant_id_short_code",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_loose",
                table: "products");

            migrationBuilder.DropColumn(
                name: "short_code",
                table: "products");
        }
    }
}
