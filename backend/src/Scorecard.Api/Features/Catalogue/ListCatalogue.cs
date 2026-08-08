using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Infrastructure.Localization;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Catalogue;

public sealed record FactorDto(
    string Code, string Name, string ShortName, string Description,
    FactorCategory Category, FactorScope Scope, int DisplayOrder);

public sealed record IndicatorDto(
    string Code, string FactorCode, string Name, string Description,
    string WhyItMatters, string HowItAffects, FactorCategory Category,
    decimal BandMinor, decimal BandMajor, int MaxAgeDays, Impact Impact);

public sealed record ProfileWeightDto(string FactorCode, decimal Weight, short Polarity, bool IsEnabled);

public sealed record ActiveProfileDto(
    string Name, int Version, Market Market,
    decimal BullishThreshold, decimal BearishThreshold, decimal MinCoverage,
    IReadOnlyList<ProfileWeightDto> Weights);

/// <summary>
/// The catalogue endpoints exist because transparency is the product promise:
/// a user should be able to read the exact weights that produced their number.
/// </summary>
public sealed class CatalogueEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/factors", async (AppDbContext db, ILocaleContext locale, CancellationToken ct) =>
        {
            var factors = await db.Factors.AsNoTracking().OrderBy(f => f.DisplayOrder).ToListAsync(ct);
            return Results.Ok(factors.Select(f => new FactorDto(
                f.Code,
                f.Name.For(locale.Locale),
                f.ShortName.For(locale.Locale),
                f.Description.For(locale.Locale),
                f.Category,
                f.Scope,
                f.DisplayOrder)));
        }).WithName("ListFactors").WithTags("Catalogue").Produces<IEnumerable<FactorDto>>();

        app.MapGet("/api/v1/indicators", async (AppDbContext db, ILocaleContext locale, CancellationToken ct) =>
        {
            var indicators = await db.Indicators.AsNoTracking().OrderBy(i => i.Code).ToListAsync(ct);
            return Results.Ok(indicators.Select(i => new IndicatorDto(
                i.Code,
                i.FactorCode,
                i.Name.For(locale.Locale),
                i.Description.For(locale.Locale),
                i.WhyItMatters.For(locale.Locale),
                i.HowItAffects.For(locale.Locale),
                i.Category,
                i.BandMinor,
                i.BandMajor,
                i.MaxAgeDays,
                i.Impact)));
        }).WithName("ListIndicators").WithTags("Catalogue").Produces<IEnumerable<IndicatorDto>>();

        app.MapGet("/api/v1/meta/profiles/{market}/active", async (
            Market market, AppDbContext db, CancellationToken ct) =>
        {
            var profile = await db.ScoringProfiles.AsNoTracking()
                .Include(p => p.Weights)
                .FirstOrDefaultAsync(p => p.Market == market && p.IsActive, ct);

            if (profile is null) return Results.NotFound();

            return Results.Ok(new ActiveProfileDto(
                profile.Name, profile.Version, profile.Market,
                profile.BullishThreshold, profile.BearishThreshold, profile.MinCoverage,
                profile.Weights
                    .OrderByDescending(w => w.Weight)
                    .Select(w => new ProfileWeightDto(w.FactorCode, w.Weight, w.Polarity, w.IsEnabled))
                    .ToList()));
        }).WithName("GetActiveProfile").WithTags("Catalogue").Produces<ActiveProfileDto>();
    }
}
