using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class AddStockRepack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_repack_recipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_qty_per_target = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_repack_recipes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_repack_recipes_products_source_product_id",
                        column: x => x.source_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_repack_recipes_products_target_product_id",
                        column: x => x.target_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_repack_recipes_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_repacks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_qty_produced = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    source_qty_consumed = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_repacks", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_repacks_product_repack_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "product_repack_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_repacks_products_source_product_id",
                        column: x => x.source_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_repacks_products_target_product_id",
                        column: x => x.target_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_repacks_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_repacks_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_repack_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_repack_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_source = table.Column<bool>(type: "boolean", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    unit_cost_paisa = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_repack_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_repack_lines_product_batches_product_batch_id",
                        column: x => x.product_batch_id,
                        principalTable: "product_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_repack_lines_stock_repacks_stock_repack_id",
                        column: x => x.stock_repack_id,
                        principalTable: "stock_repacks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_repack_lines_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_repack_recipes_source_product_id",
                table: "product_repack_recipes",
                column: "source_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_repack_recipes_target_product_id",
                table: "product_repack_recipes",
                column: "target_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_repack_recipes_tenant_id",
                table: "product_repack_recipes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_repack_recipes_tenant_id_source_product_id_target_p~",
                table: "product_repack_recipes",
                columns: new[] { "tenant_id", "source_product_id", "target_product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_repack_lines_product_batch_id",
                table: "stock_repack_lines",
                column: "product_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_repack_lines_stock_repack_id",
                table: "stock_repack_lines",
                column: "stock_repack_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_repack_lines_tenant_id",
                table: "stock_repack_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_repacks_performed_by_user_id",
                table: "stock_repacks",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_repacks_recipe_id",
                table: "stock_repacks",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_repacks_source_product_id",
                table: "stock_repacks",
                column: "source_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_repacks_target_product_id",
                table: "stock_repacks",
                column: "target_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_repacks_tenant_id",
                table: "stock_repacks",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_repack_lines");

            migrationBuilder.DropTable(
                name: "stock_repacks");

            migrationBuilder.DropTable(
                name: "product_repack_recipes");
        }
    }
}
