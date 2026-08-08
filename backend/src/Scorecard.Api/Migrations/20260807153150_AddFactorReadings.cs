using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scorecard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFactorReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "readings",
                table: "asset_factor_scores",
                type: "jsonb",
                nullable: false,
                // '[]', not '': an empty string is not valid JSON and Postgres
                // rejects the cast outright. Rows written before this column
                // existed legitimately carry no readings.
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "readings",
                table: "asset_factor_scores");
        }
    }
}
