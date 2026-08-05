using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class MasterFinanceNullableExpenseShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shift_expenses_cashier_shifts_shift_id",
                table: "shift_expenses");

            migrationBuilder.AlterColumn<Guid>(
                name: "shift_id",
                table: "shift_expenses",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_shift_expenses_cashier_shifts_shift_id",
                table: "shift_expenses",
                column: "shift_id",
                principalTable: "cashier_shifts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shift_expenses_cashier_shifts_shift_id",
                table: "shift_expenses");

            migrationBuilder.AlterColumn<Guid>(
                name: "shift_id",
                table: "shift_expenses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_expenses_cashier_shifts_shift_id",
                table: "shift_expenses",
                column: "shift_id",
                principalTable: "cashier_shifts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
