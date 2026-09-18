using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebPos.Core.Data;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(WebPosDbContext))]
    [Migration("20260914160000_AddCashAccountBalancePaisa")]
    public partial class AddCashAccountBalancePaisa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "balance_paisa",
                table: "cash_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE cash_accounts AS ca
                SET balance_paisa = COALESCE((
                    SELECT SUM(gle.debit_paisa) - SUM(gle.credit_paisa)
                    FROM general_ledger_entries AS gle
                    WHERE gle.tenant_id = ca.tenant_id
                      AND UPPER(gle.account_code) = UPPER(ca.account_code)
                ), 0);
                """);

            migrationBuilder.Sql(
                """
                UPDATE cash_accounts
                SET balance_paisa = 0
                WHERE balance_paisa < 0;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_cash_accounts_balance_paisa_non_negative",
                table: "cash_accounts",
                sql: "\"balance_paisa\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_cash_accounts_balance_paisa_non_negative",
                table: "cash_accounts");

            migrationBuilder.DropColumn(
                name: "balance_paisa",
                table: "cash_accounts");
        }
    }
}
