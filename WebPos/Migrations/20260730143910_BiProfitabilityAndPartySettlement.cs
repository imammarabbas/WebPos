using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class BiProfitabilityAndPartySettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "unit_cost_paisa",
                table: "sales_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "amount_paid_paisa",
                table: "sales_invoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "amount_paid_paisa",
                table: "purchase_orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "credit_paisa",
                table: "party_ledgers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "debit_paisa",
                table: "party_ledgers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "party_payment_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_ledger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_paisa = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_party_payment_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_party_payment_allocations_party_ledgers_party_ledger_id",
                        column: x => x.party_ledger_id,
                        principalTable: "party_ledgers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_party_payment_allocations_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_party_payment_allocations_sales_invoices_invoice_no",
                        column: x => x.invoice_no,
                        principalTable: "sales_invoices",
                        principalColumn: "invoice_no",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_party_payment_allocations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_party_payment_allocations_invoice_no",
                table: "party_payment_allocations",
                column: "invoice_no");

            migrationBuilder.CreateIndex(
                name: "IX_party_payment_allocations_party_ledger_id",
                table: "party_payment_allocations",
                column: "party_ledger_id");

            migrationBuilder.CreateIndex(
                name: "IX_party_payment_allocations_purchase_order_id",
                table: "party_payment_allocations",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_party_payment_allocations_tenant_id",
                table: "party_payment_allocations",
                column: "tenant_id");

            // Backfill snapshotted unit cost from the batch used on each sale line.
            migrationBuilder.Sql("""
                UPDATE sales_items AS si
                SET unit_cost_paisa = pb.cost_price_paisa
                FROM product_batches AS pb
                WHERE si.batch_id = pb.id;
                """);

            // Mark non-credit invoices as fully paid so open-slip queries stay clean.
            migrationBuilder.Sql("""
                UPDATE sales_invoices
                SET amount_paid_paisa = total_amount_paisa
                WHERE UPPER(payment_method) <> 'CREDIT';
                """);

            // Reconstruct debit/credit from balance movement on existing ledger rows.
            migrationBuilder.Sql("""
                UPDATE party_ledgers
                SET
                    debit_paisa = CASE
                        WHEN running_balance_paisa > old_balance_paisa THEN amount_paisa
                        ELSE 0
                    END,
                    credit_paisa = CASE
                        WHEN running_balance_paisa < old_balance_paisa THEN amount_paisa
                        ELSE 0
                    END;
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE VIEW bi_product_profitability AS
                SELECT
                    si.tenant_id,
                    si.product_id,
                    p.name AS product_name,
                    ROUND(SUM(si.quantity * si.unit_price_paisa))::bigint AS revenue_paisa,
                    ROUND(SUM(si.quantity * si.unit_cost_paisa))::bigint AS cost_paisa,
                    ROUND(SUM(si.quantity * si.unit_price_paisa) - SUM(si.quantity * si.unit_cost_paisa))::bigint AS gross_profit_paisa,
                    CASE
                        WHEN SUM(si.quantity * si.unit_price_paisa) = 0 THEN 0
                        ELSE ROUND(
                            ((SUM(si.quantity * si.unit_price_paisa) - SUM(si.quantity * si.unit_cost_paisa)) * 100.0)
                            / SUM(si.quantity * si.unit_price_paisa),
                            2)
                    END AS margin_percent,
                    MIN(inv.created_at) AS first_sale_at,
                    MAX(inv.created_at) AS last_sale_at
                FROM sales_items AS si
                INNER JOIN products AS p ON p.id = si.product_id
                INNER JOIN sales_invoices AS inv ON inv.invoice_no = si.invoice_no
                GROUP BY si.tenant_id, si.product_id, p.name;
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE VIEW bi_category_profitability AS
                SELECT
                    si.tenant_id,
                    c.id AS category_id,
                    c.name AS category_name,
                    ROUND(SUM(si.quantity * si.unit_price_paisa))::bigint AS revenue_paisa,
                    ROUND(SUM(si.quantity * si.unit_cost_paisa))::bigint AS cost_paisa,
                    ROUND(SUM(si.quantity * si.unit_price_paisa) - SUM(si.quantity * si.unit_cost_paisa))::bigint AS gross_profit_paisa,
                    CASE
                        WHEN SUM(si.quantity * si.unit_price_paisa) = 0 THEN 0
                        ELSE ROUND(
                            ((SUM(si.quantity * si.unit_price_paisa) - SUM(si.quantity * si.unit_cost_paisa)) * 100.0)
                            / SUM(si.quantity * si.unit_price_paisa),
                            2)
                    END AS margin_percent
                FROM sales_items AS si
                INNER JOIN products AS p ON p.id = si.product_id
                INNER JOIN categories AS c ON c.id = p.category_id
                INNER JOIN sales_invoices AS inv ON inv.invoice_no = si.invoice_no
                WHERE p.category_id IS NOT NULL
                GROUP BY si.tenant_id, c.id, c.name;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS bi_category_profitability;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS bi_product_profitability;");

            migrationBuilder.DropTable(
                name: "party_payment_allocations");

            migrationBuilder.DropColumn(
                name: "unit_cost_paisa",
                table: "sales_items");

            migrationBuilder.DropColumn(
                name: "amount_paid_paisa",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "amount_paid_paisa",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "credit_paisa",
                table: "party_ledgers");

            migrationBuilder.DropColumn(
                name: "debit_paisa",
                table: "party_ledgers");
        }
    }
}
