using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_margin_percentage = table.Column<int>(type: "integer", nullable: false),
                    show_on_webshop = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "general_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    debit_paisa = table.Column<long>(type: "bigint", nullable: false),
                    credit_paisa = table.Column<long>(type: "bigint", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reference_details = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_general_ledger_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    credit_limit_paisa = table.Column<long>(type: "bigint", nullable: false),
                    current_balance_paisa = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "terminals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mac_address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_sync_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    base_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    conversion_multiplier = table.Column<int>(type: "integer", nullable: false),
                    show_on_webshop = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_milk_collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    milk_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    liters_received = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    rate_per_liter_paisa = table.Column<long>(type: "bigint", nullable: false),
                    total_credit_paisa = table.Column<long>(type: "bigint", nullable: false),
                    collection_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_milk_collections", x => x.id);
                    table.ForeignKey(
                        name: "FK_daily_milk_collections_parties_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cashier_shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cashier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    opening_cash_paisa = table.Column<long>(type: "bigint", nullable: false),
                    expected_cash_paisa = table.Column<long>(type: "bigint", nullable: false),
                    actual_blind_cash_paisa = table.Column<long>(type: "bigint", nullable: true),
                    discrepancy_paisa = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cashier_shifts", x => x.id);
                    table.ForeignKey(
                        name: "FK_cashier_shifts_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalTable: "terminals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cashier_shifts_users_cashier_id",
                        column: x => x.cashier_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    additional_overhead_paisa = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_production_logs_users_operator_id",
                        column: x => x.operator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sub_total_paisa = table.Column<long>(type: "bigint", nullable: false),
                    discount_paisa = table.Column<long>(type: "bigint", nullable: false),
                    net_payable_paisa = table.Column<long>(type: "bigint", nullable: false),
                    payment_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_parties_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_receiver_id",
                        column: x => x.receiver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                columns: table => new
                {
                    invoice_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cashier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_amount_paisa = table.Column<long>(type: "bigint", nullable: false),
                    tax_amount_paisa = table.Column<long>(type: "bigint", nullable: false),
                    discount_amount_paisa = table.Column<long>(type: "bigint", nullable: false),
                    discount_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    receipt_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoices", x => x.invoice_no);
                    table.ForeignKey(
                        name: "FK_sales_invoices_cashier_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "cashier_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoices_parties_customer_id",
                        column: x => x.customer_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoices_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalTable: "terminals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoices_users_cashier_id",
                        column: x => x.cashier_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shift_expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    amount_paisa = table.Column<long>(type: "bigint", nullable: false),
                    expense_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    receipt_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_recurring = table.Column<bool>(type: "boolean", nullable: false),
                    logged_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_expenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_expenses_cashier_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "cashier_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shift_expenses_users_logged_by_user_id",
                        column: x => x.logged_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_consumption_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_consumed = table.Column<decimal>(type: "numeric(12,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_consumption_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_production_consumption_items_production_logs_production_log~",
                        column: x => x.production_log_id,
                        principalTable: "production_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_production_consumption_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    cost_price_paisa = table.Column<long>(type: "bigint", nullable: false),
                    retail_price_paisa = table.Column<long>(type: "bigint", nullable: false),
                    initial_qty = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    current_qty = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rack_location = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_batches_parties_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_batches_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_batches_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_credit_deduction_paisa = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_returns_parties_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_purchase_orders_original_purchase_order_id",
                        column: x => x.original_purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_users_manager_id",
                        column: x => x.manager_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "party_ledgers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    old_balance_paisa = table.Column<long>(type: "bigint", nullable: false),
                    amount_paisa = table.Column<long>(type: "bigint", nullable: false),
                    running_balance_paisa = table.Column<long>(type: "bigint", nullable: false),
                    reference_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_party_ledgers", x => x.id);
                    table.ForeignKey(
                        name: "FK_party_ledgers_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_party_ledgers_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_party_ledgers_sales_invoices_invoice_no",
                        column: x => x.invoice_no,
                        principalTable: "sales_invoices",
                        principalColumn: "invoice_no",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_invoice_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cashier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_refund_paisa = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_returns_parties_customer_id",
                        column: x => x.customer_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_sales_invoices_original_invoice_no",
                        column: x => x.original_invoice_no,
                        principalTable: "sales_invoices",
                        principalColumn: "invoice_no",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_users_cashier_id",
                        column: x => x.cashier_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "damaged_stock_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    logged_by = table.Column<Guid>(type: "uuid", nullable: false),
                    write_off_loss_paisa = table.Column<long>(type: "bigint", nullable: false),
                    logged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_damaged_stock_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_damaged_stock_logs_product_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "product_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_damaged_stock_logs_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_damaged_stock_logs_users_logged_by",
                        column: x => x.logged_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_yield_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_produced = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    calculated_cost_price_paisa = table.Column<long>(type: "bigint", nullable: false),
                    target_batch_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_yield_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_production_yield_items_product_batches_target_batch_id",
                        column: x => x.target_batch_id,
                        principalTable: "product_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_yield_items_production_logs_production_log_id",
                        column: x => x.production_log_id,
                        principalTable: "production_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_production_yield_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_received = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    bonus_quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    cost_price_per_unit_paisa = table.Column<long>(type: "bigint", nullable: false),
                    retail_price_per_unit_paisa = table.Column<long>(type: "bigint", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_items_product_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "product_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_items_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    unit_price_paisa = table.Column<long>(type: "bigint", nullable: false),
                    discount_applied_paisa = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_items_product_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "product_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_items_sales_invoices_invoice_no",
                        column: x => x.invoice_no,
                        principalTable: "sales_invoices",
                        principalColumn: "invoice_no",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_return_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    cost_per_unit_paisa = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_return_items_product_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "product_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_return_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_return_items_purchase_returns_purchase_return_id",
                        column: x => x.purchase_return_id,
                        principalTable: "purchase_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    refund_unit_price_paisa = table.Column<long>(type: "bigint", nullable: false),
                    return_condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_items_product_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "product_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_return_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_return_items_sales_returns_sales_return_id",
                        column: x => x.sales_return_id,
                        principalTable: "sales_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_cashier_id",
                table: "cashier_shifts",
                column: "cashier_id");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_terminal_id",
                table: "cashier_shifts",
                column: "terminal_id");

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_category_id",
                table: "categories",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_milk_collections_supplier_id",
                table: "daily_milk_collections",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_damaged_stock_logs_batch_id",
                table: "damaged_stock_logs",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_damaged_stock_logs_logged_by",
                table: "damaged_stock_logs",
                column: "logged_by");

            migrationBuilder.CreateIndex(
                name: "IX_damaged_stock_logs_product_id",
                table: "damaged_stock_logs",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_account_code",
                table: "general_ledger_entries",
                column: "account_code");

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_created_at",
                table: "general_ledger_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_reference_no",
                table: "general_ledger_entries",
                column: "reference_no");

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_transaction_group_id",
                table: "general_ledger_entries",
                column: "transaction_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_parties_phone_number",
                table: "parties",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_party_ledgers_invoice_no",
                table: "party_ledgers",
                column: "invoice_no");

            migrationBuilder.CreateIndex(
                name: "IX_party_ledgers_party_id",
                table: "party_ledgers",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "IX_party_ledgers_purchase_order_id",
                table: "party_ledgers",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_product_id",
                table: "product_batches",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_purchase_order_id",
                table: "product_batches",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_batches_supplier_id",
                table: "product_batches",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_consumption_items_product_id",
                table: "production_consumption_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_consumption_items_production_log_id",
                table: "production_consumption_items",
                column: "production_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_logs_batch_reference",
                table: "production_logs",
                column: "batch_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_logs_operator_id",
                table: "production_logs",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_yield_items_product_id",
                table: "production_yield_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_yield_items_production_log_id",
                table: "production_yield_items",
                column: "production_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_yield_items_target_batch_id",
                table: "production_yield_items",
                column: "target_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_barcode",
                table: "products",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sku",
                table: "products",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_items_batch_id",
                table: "purchase_items",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_items_product_id",
                table: "purchase_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_items_purchase_order_id",
                table: "purchase_items",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_receiver_id",
                table: "purchase_orders",
                column: "receiver_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id",
                table: "purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_items_batch_id",
                table: "purchase_return_items",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_items_product_id",
                table: "purchase_return_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_items_purchase_return_id",
                table: "purchase_return_items",
                column: "purchase_return_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_manager_id",
                table: "purchase_returns",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_original_purchase_order_id",
                table: "purchase_returns",
                column: "original_purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_supplier_id",
                table: "purchase_returns",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_role_name",
                table: "roles",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_cashier_id",
                table: "sales_invoices",
                column: "cashier_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_customer_id",
                table: "sales_invoices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_shift_id",
                table: "sales_invoices",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_terminal_id",
                table: "sales_invoices",
                column: "terminal_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_items_batch_id",
                table: "sales_items",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_items_invoice_no",
                table: "sales_items",
                column: "invoice_no");

            migrationBuilder.CreateIndex(
                name: "IX_sales_items_product_id",
                table: "sales_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_items_batch_id",
                table: "sales_return_items",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_items_product_id",
                table: "sales_return_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_items_sales_return_id",
                table: "sales_return_items",
                column: "sales_return_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_cashier_id",
                table: "sales_returns",
                column: "cashier_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_customer_id",
                table: "sales_returns",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_original_invoice_no",
                table: "sales_returns",
                column: "original_invoice_no");

            migrationBuilder.CreateIndex(
                name: "IX_shift_expenses_expense_category",
                table: "shift_expenses",
                column: "expense_category");

            migrationBuilder.CreateIndex(
                name: "IX_shift_expenses_logged_by_user_id",
                table: "shift_expenses",
                column: "logged_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_expenses_shift_id",
                table: "shift_expenses",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_expenses_voucher_no",
                table: "shift_expenses",
                column: "voucher_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terminals_mac_address",
                table: "terminals",
                column: "mac_address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terminals_terminal_name",
                table: "terminals",
                column: "terminal_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_milk_collections");

            migrationBuilder.DropTable(
                name: "damaged_stock_logs");

            migrationBuilder.DropTable(
                name: "general_ledger_entries");

            migrationBuilder.DropTable(
                name: "party_ledgers");

            migrationBuilder.DropTable(
                name: "production_consumption_items");

            migrationBuilder.DropTable(
                name: "production_yield_items");

            migrationBuilder.DropTable(
                name: "purchase_items");

            migrationBuilder.DropTable(
                name: "purchase_return_items");

            migrationBuilder.DropTable(
                name: "sales_items");

            migrationBuilder.DropTable(
                name: "sales_return_items");

            migrationBuilder.DropTable(
                name: "shift_expenses");

            migrationBuilder.DropTable(
                name: "production_logs");

            migrationBuilder.DropTable(
                name: "purchase_returns");

            migrationBuilder.DropTable(
                name: "product_batches");

            migrationBuilder.DropTable(
                name: "sales_returns");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "sales_invoices");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "cashier_shifts");

            migrationBuilder.DropTable(
                name: "parties");

            migrationBuilder.DropTable(
                name: "terminals");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
