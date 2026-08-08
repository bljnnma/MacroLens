using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scorecard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesScaleNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scale_note",
                table: "market_series",
                type: "jsonb",
                nullable: false,
                // A LocalizedText with empty halves, not '': an empty string is
                // not valid JSON and Postgres rejects the cast. Existing rows get
                // no qualifier, which is the correct default — only the dollar
                // index needs one.
                defaultValue: """{"mn":"","en":""}""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scale_note",
                table: "market_series");
        }
    }
}
