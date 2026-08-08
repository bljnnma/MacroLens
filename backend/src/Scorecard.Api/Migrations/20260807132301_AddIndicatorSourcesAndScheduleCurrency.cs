using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scorecard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicatorSourcesAndScheduleCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sync_schedules_source_kind_source_code",
                table: "sync_schedules");

            migrationBuilder.AddColumn<string>(
                name: "source_currency",
                table: "sync_schedules",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            // Existing indicator schedules were all USD — C3a's only scope. Naming
            // them keeps their poll history instead of letting reconciliation drop
            // them as orphans and re-create them due immediately. Market series
            // keep the empty default: they have no currency dimension.
            migrationBuilder.Sql(
                "UPDATE sync_schedules SET source_currency = 'USD' WHERE source_kind = 2;");

            migrationBuilder.AddColumn<short>(
                name: "cadence",
                table: "market_series",
                type: "smallint",
                nullable: false,
                // 1 = Daily, the domain default. EF would have written 0, which
                // is not a member of SyncCadence at all.
                defaultValue: (short)1);

            // Backfill the one series whose publication cadence is not its
            // observation frequency. Without this the existing DXY schedule is
            // re-tuned to the wrong cadence on the next startup — the exact bug
            // the enum was added to prevent, reintroduced by a migration default.
            migrationBuilder.Sql(
                "UPDATE market_series SET cadence = 2 WHERE code = 'DXY';");

            migrationBuilder.CreateTable(
                name: "indicator_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    indicator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    provider = table.Column<short>(type: "smallint", nullable: false),
                    provider_series_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    transform = table.Column<short>(type: "smallint", nullable: false),
                    cadence = table.Column<short>(type: "smallint", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_indicator_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_indicator_sources_indicators_indicator_id",
                        column: x => x.indicator_id,
                        principalTable: "indicators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sync_schedules_source_kind_source_code_source_currency",
                table: "sync_schedules",
                columns: new[] { "source_kind", "source_code", "source_currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_indicator_sources_indicator_id_currency_code",
                table: "indicator_sources",
                columns: new[] { "indicator_id", "currency_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "indicator_sources");

            migrationBuilder.DropIndex(
                name: "ix_sync_schedules_source_kind_source_code_source_currency",
                table: "sync_schedules");

            migrationBuilder.DropColumn(
                name: "source_currency",
                table: "sync_schedules");

            migrationBuilder.DropColumn(
                name: "cadence",
                table: "market_series");

            migrationBuilder.CreateIndex(
                name: "ix_sync_schedules_source_kind_source_code",
                table: "sync_schedules",
                columns: new[] { "source_kind", "source_code" },
                unique: true);
        }
    }
}
