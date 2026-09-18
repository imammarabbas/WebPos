using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftCashMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shift_cash_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    till_cash_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    amount_paisa = table.Column<long>(type: "bigint", nullable: false),
                    align_paisa = table.Column<long>(type: "bigint", nullable: false),
                    excess_paisa = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    linked_reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    linked_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_cash_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_cash_movements_cash_accounts_till_cash_account_id",
                        column: x => x.till_cash_account_id,
                        principalTable: "cash_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_cash_movements_cashier_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "cashier_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_cash_movements_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shift_cash_movements_shift_id_status",
                table: "shift_cash_movements",
                columns: new[] { "shift_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_shift_cash_movements_tenant_id",
                table: "shift_cash_movements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_cash_movements_till_cash_account_id",
                table: "shift_cash_movements",
                column: "till_cash_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shift_cash_movements");
        }
    }
}
