using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations;

/// <summary>
/// Expands the schema with nullable tenant columns, backfills the existing shop
/// to the Master Tenant, then contracts the columns to required foreign keys.
/// EF Core/Npgsql runs this migration inside one database transaction.
/// </summary>
public partial class AddMultiTenantFoundation : Migration
{
    private const string MasterTenantId = "00000000-0000-0000-0000-000000000001";

    private static readonly string[] TenantTables =
    [
        "roles",
        "users",
        "terminals",
        "cashier_shifts",
        "shift_expenses",
        "parties",
        "party_ledgers",
        "general_ledger_entries",
        "categories",
        "products",
        "purchase_orders",
        "product_batches",
        "purchase_items",
        "daily_milk_collections",
        "production_logs",
        "production_consumption_items",
        "production_yield_items",
        "sales_invoices",
        "sales_items",
        "sales_returns",
        "sales_return_items",
        "purchase_returns",
        "purchase_return_items",
        "damaged_stock_logs"
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                slug = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenants", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tenants_slug",
            table: "tenants",
            column: "slug",
            unique: true);

        // Expand: existing rows remain valid while tenant_id is nullable.
        foreach (string table in TenantTables)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: table,
                type: "uuid",
                nullable: true);
        }

        migrationBuilder.InsertData(
            table: "tenants",
            columns: ["id", "name", "slug", "is_active", "created_at"],
            values:
            [
                Guid.Parse(MasterTenantId),
                "Master Tenant",
                "master",
                true,
                new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero)
            ]);

        // Backfill: every statement participates in the migration transaction.
        foreach (string table in TenantTables)
        {
            migrationBuilder.Sql(
                $"""
                 UPDATE "{table}"
                 SET "tenant_id" = '{MasterTenantId}'::uuid
                 WHERE "tenant_id" IS NULL;
                 """,
                suppressTransaction: false);
        }

        // Contract: only enforce NOT NULL after every existing row is assigned.
        foreach (string table in TenantTables)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: table,
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        foreach (string table in TenantTables)
        {
            migrationBuilder.CreateIndex(
                name: $"IX_{table}_tenant_id",
                table: table,
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: $"FK_{table}_tenants_tenant_id",
                table: table,
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (string table in TenantTables)
        {
            migrationBuilder.DropForeignKey(
                name: $"FK_{table}_tenants_tenant_id",
                table: table);

            migrationBuilder.DropIndex(
                name: $"IX_{table}_tenant_id",
                table: table);

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: table);
        }

        migrationBuilder.DropTable(name: "tenants");
    }
}
