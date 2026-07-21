using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopedPartyUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_parties_phone_number",
                table: "parties");

            migrationBuilder.CreateIndex(
                name: "UX_parties_tenant_phone",
                table: "parties",
                columns: new[] { "tenant_id", "phone_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_parties_tenant_type_name",
                table: "parties",
                columns: new[] { "tenant_id", "party_type", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_parties_tenant_phone",
                table: "parties");

            migrationBuilder.DropIndex(
                name: "UX_parties_tenant_type_name",
                table: "parties");

            // Explicitly drop the old global index before restoring prior state
            // to prevent "already exists" conflicts on rollback.
            migrationBuilder.Sql(
                """DROP INDEX IF EXISTS "IX_parties_phone_number";""");

            migrationBuilder.CreateIndex(
                name: "IX_parties_phone_number",
                table: "parties",
                column: "phone_number",
                unique: true);
        }
    }
}
