using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseReceiveAndNullableBatchExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_received",
                table: "purchase_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "batch_number",
                table: "purchase_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "expiry_date",
                table: "purchase_items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rack_location",
                table: "purchase_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "expiry_date",
                table: "product_batches",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            // Existing credit POs already posted inventory; mark them received so
            // ReceiveStockAsync cannot double-post historical invoices.
            migrationBuilder.Sql(
                """
                UPDATE purchase_orders
                SET is_received = TRUE
                WHERE payment_status = 'CREDIT'
                   OR EXISTS (
                        SELECT 1
                        FROM purchase_items pi
                        WHERE pi.purchase_order_id = purchase_orders.id
                          AND pi.batch_id IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_received",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "batch_number",
                table: "purchase_items");

            migrationBuilder.DropColumn(
                name: "expiry_date",
                table: "purchase_items");

            migrationBuilder.DropColumn(
                name: "rack_location",
                table: "purchase_items");

            migrationBuilder.Sql(
                """
                UPDATE product_batches
                SET expiry_date = DATE '0001-01-01'
                WHERE expiry_date IS NULL;
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "expiry_date",
                table: "product_batches",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
