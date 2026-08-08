using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scorecard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currency_policies",
                columns: table => new
                {
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    inflation_target = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    tolerance_band = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    authority = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currency_policies", x => x.currency_code);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "currency_policies");
        }
    }
}
