using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCatalogSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_name_active",
                table: "products",
                columns: new[] { "tenant_id", "name" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_bulk_parents",
                table: "products",
                columns: new[] { "tenant_id", "name" },
                filter: "is_bulk AND parent_product_id IS NULL AND is_deleted = FALSE");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS IX_products_name_trgm
                    ON products USING gin (name gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS IX_products_sku_trgm
                    ON products USING gin (sku gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS IX_products_barcode_trgm
                    ON products USING gin (barcode gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS IX_products_brand_trgm
                    ON products USING gin (brand gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS IX_products_name_trgm;
                DROP INDEX IF EXISTS IX_products_sku_trgm;
                DROP INDEX IF EXISTS IX_products_barcode_trgm;
                DROP INDEX IF EXISTS IX_products_brand_trgm;
                """);

            migrationBuilder.DropIndex(
                name: "IX_products_tenant_bulk_parents",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_tenant_name_active",
                table: "products");
        }
    }
}
