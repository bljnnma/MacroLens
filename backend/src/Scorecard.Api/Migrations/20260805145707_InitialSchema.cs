using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scorecard.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false),
                    market = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "factors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false),
                    short_name = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    category = table.Column<short>(type: "smallint", nullable: false),
                    scope = table.Column<short>(type: "smallint", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_factors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "indicators",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    factor_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    why_it_matters = table.Column<string>(type: "jsonb", nullable: false),
                    how_it_affects = table.Column<string>(type: "jsonb", nullable: false),
                    category = table.Column<short>(type: "smallint", nullable: false),
                    currency_direction = table.Column<short>(type: "smallint", nullable: false),
                    impact = table.Column<short>(type: "smallint", nullable: false),
                    unit = table.Column<short>(type: "smallint", nullable: false),
                    band_minor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    band_major = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    max_age_days = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_indicators", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "market_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    factor_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    unit = table.Column<short>(type: "smallint", nullable: false),
                    frequency = table.Column<short>(type: "smallint", nullable: false),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    max_age_days = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scoring_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    market = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    bullish_threshold = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    bearish_threshold = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    min_coverage = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scoring_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_currency_exposures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    direction = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_currency_exposures", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_currency_exposures_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "indicator_releases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    indicator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    period = table.Column<DateOnly>(type: "date", nullable: false),
                    actual = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    forecast = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    previous = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    source_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_indicator_releases", x => x.id);
                    table.ForeignKey(
                        name: "fk_indicator_releases_indicators_indicator_id",
                        column: x => x.indicator_id,
                        principalTable: "indicators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "series_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    source = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_series_observations_market_series_series_id",
                        column: x => x.series_id,
                        principalTable: "market_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    bias = table.Column<short>(type: "smallint", nullable: false),
                    coverage = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    is_sufficient = table.Column<bool>(type: "boolean", nullable: false),
                    scoring_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_version = table.Column<int>(type: "integer", nullable: false),
                    engine_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_as_of = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    calculation_duration_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_scores_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_scores_scoring_profiles_scoring_profile_id",
                        column: x => x.scoring_profile_id,
                        principalTable: "scoring_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profile_weights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    polarity = table.Column<short>(type: "smallint", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_weights", x => x.id);
                    table.ForeignKey(
                        name: "fk_profile_weights_scoring_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "scoring_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_factor_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_score_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    raw_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    raw_label_mn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    raw_label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_score = table.Column<short>(type: "smallint", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    polarity = table.Column<short>(type: "smallint", nullable: false),
                    contribution = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    explanation_mn = table.Column<string>(type: "text", nullable: false),
                    explanation_en = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_factor_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_factor_scores_asset_scores_asset_score_id",
                        column: x => x.asset_score_id,
                        principalTable: "asset_scores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asset_currency_exposures_asset_id_currency_code",
                table: "asset_currency_exposures",
                columns: new[] { "asset_id", "currency_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_factor_scores_asset_score_id",
                table: "asset_factor_scores",
                column: "asset_score_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_scores_asset_id_calculated_at",
                table: "asset_scores",
                columns: new[] { "asset_id", "calculated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_asset_scores_scoring_profile_id",
                table: "asset_scores",
                column: "scoring_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_scores_sufficient_calculated_at",
                table: "asset_scores",
                column: "calculated_at",
                descending: new bool[0],
                filter: "is_sufficient");

            migrationBuilder.CreateIndex(
                name: "ix_assets_symbol",
                table: "assets",
                column: "symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_factors_code",
                table: "factors",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_indicator_releases_currency_code_released_at",
                table: "indicator_releases",
                columns: new[] { "currency_code", "released_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_indicator_releases_indicator_id_currency_code_period_revisi",
                table: "indicator_releases",
                columns: new[] { "indicator_id", "currency_code", "period", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_indicators_code",
                table: "indicators",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_indicators_factor_code",
                table: "indicators",
                column: "factor_code");

            migrationBuilder.CreateIndex(
                name: "ix_market_series_code",
                table: "market_series",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profile_weights_profile_id_factor_code",
                table: "profile_weights",
                columns: new[] { "profile_id", "factor_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scoring_profiles_market_version",
                table: "scoring_profiles",
                columns: new[] { "market", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_scoring_profiles_active_per_market",
                table: "scoring_profiles",
                column: "market",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_series_observations_series_id_observed_at",
                table: "series_observations",
                columns: new[] { "series_id", "observed_at" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_currency_exposures");

            migrationBuilder.DropTable(
                name: "asset_factor_scores");

            migrationBuilder.DropTable(
                name: "factors");

            migrationBuilder.DropTable(
                name: "indicator_releases");

            migrationBuilder.DropTable(
                name: "profile_weights");

            migrationBuilder.DropTable(
                name: "series_observations");

            migrationBuilder.DropTable(
                name: "asset_scores");

            migrationBuilder.DropTable(
                name: "indicators");

            migrationBuilder.DropTable(
                name: "market_series");

            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "scoring_profiles");
        }
    }
}
