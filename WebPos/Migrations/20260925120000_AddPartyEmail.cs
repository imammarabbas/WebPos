using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebPos.Core.Data;

#nullable disable

namespace WebPos.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(WebPosDbContext))]
    [Migration("20260925120000_AddPartyEmail")]
    public partial class AddPartyEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "parties",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                table: "parties");
        }
    }
}
