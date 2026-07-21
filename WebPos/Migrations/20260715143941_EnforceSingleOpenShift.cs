using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleOpenShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cashier_shifts_cashier_id",
                table: "cashier_shifts");

            migrationBuilder.DropIndex(
                name: "IX_cashier_shifts_terminal_id",
                table: "cashier_shifts");

            migrationBuilder.CreateIndex(
                name: "UX_cashier_shifts_open_cashier",
                table: "cashier_shifts",
                column: "cashier_id",
                unique: true,
                filter: "\"status\" = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "UX_cashier_shifts_open_terminal",
                table: "cashier_shifts",
                column: "terminal_id",
                unique: true,
                filter: "\"status\" = 'OPEN'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_cashier_shifts_open_cashier",
                table: "cashier_shifts");

            migrationBuilder.DropIndex(
                name: "UX_cashier_shifts_open_terminal",
                table: "cashier_shifts");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_cashier_id",
                table: "cashier_shifts",
                column: "cashier_id");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_shifts_terminal_id",
                table: "cashier_shifts",
                column: "terminal_id");
        }
    }
}
