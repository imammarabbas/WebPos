using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class AddCashAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    account_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    payment_method_key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_accounts_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_accounts_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalTable: "terminals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_accounts_tenant_id",
                table: "cash_accounts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_accounts_tenant_type_sort",
                table: "cash_accounts",
                columns: new[] { "tenant_id", "account_type", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_accounts_terminal_id",
                table: "cash_accounts",
                column: "terminal_id");

            migrationBuilder.CreateIndex(
                name: "UX_cash_accounts_tenant_account_code",
                table: "cash_accounts",
                columns: new[] { "tenant_id", "account_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_cash_accounts_tenant_terminal",
                table: "cash_accounts",
                columns: new[] { "tenant_id", "terminal_id" },
                unique: true,
                filter: "\"terminal_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_accounts");
        }
    }
}
