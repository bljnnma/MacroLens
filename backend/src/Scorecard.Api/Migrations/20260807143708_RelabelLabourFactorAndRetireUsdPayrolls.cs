using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scorecard.Api.Migrations
{
    /// <summary>
    /// No schema change — a one-time data correction the seeder cannot make.
    ///
    /// Reference metadata (names, staleness windows) is now reconciled on every
    /// startup, but ingested FACT rows are not seed data and must be cleaned up
    /// explicitly.
    /// </summary>
    public partial class RelabelLabourFactorAndRetireUsdPayrolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // USD moved from payrolls to the harmonised unemployment rate, so the
            // whole currency universe is now scored on one comparable statistic.
            //
            // These rows have to go rather than merely stop refreshing: NFP and
            // UNEMPLOYMENT both feed the LABOUR factor, the loader picks a single
            // release per (factor, currency), and with equal periods the tiebreak
            // between two different indicators is arbitrary. Leaving them would
            // make USD's labour score depend on which row happened to sort first.
            migrationBuilder.Sql("""
                DELETE FROM indicator_releases r
                USING indicators i
                WHERE i.id = r.indicator_id
                  AND i.code = 'NFP'
                  AND r.currency_code = 'USD';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty. The deleted rows came from FRED and are
            // re-ingestable by re-enabling the mapping and running a sync;
            // reconstructing them here would invent data.
        }
    }
}
