using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scorecard.Api.Migrations
{
    /// <inheritdoc />
    public partial class HalfStepNormalizedScores : Migration
    {
        /// <summary>
        /// smallint -> numeric(2,1). Widening, so every existing whole-number
        /// score converts exactly; EF's data-loss warning is about the reverse.
        ///
        /// Down() genuinely does lose data — a stored -1.5 cannot survive a
        /// return to smallint. Rolling back past this point means those scores
        /// must be recalculated, not truncated.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "normalized_score",
                table: "asset_factor_scores",
                type: "numeric(2,1)",
                precision: 2,
                scale: 1,
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<short>(
                name: "normalized_score",
                table: "asset_factor_scores",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(2,1)",
                oldPrecision: 2,
                oldScale: 1,
                oldNullable: true);
        }
    }
}
