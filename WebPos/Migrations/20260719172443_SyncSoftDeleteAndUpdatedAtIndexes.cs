using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class SyncSoftDeleteAndUpdatedAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "parties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_updated_at",
                table: "products",
                columns: new[] { "tenant_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_parties_tenant_updated_at",
                table: "parties",
                columns: new[] { "tenant_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_tenant_updated_at",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_parties_tenant_updated_at",
                table: "parties");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "parties");
        }
    }
}
