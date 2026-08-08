using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Infrastructure.Localization;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Calendar;

public sealed record CalendarEventDto(
    string Id,
    string IndicatorCode,
    string FactorCode,
    string Title,
    string Currency,
    DateTimeOffset ReleasedAt,
    Impact Importance,
    decimal? Actual,
    decimal? Forecast,
    decimal? Previous,
    IndicatorUnit Unit,
    /// <summary>Direction of the surprise for the releasing currency; null until it prints.</summary>
    Bias? BiasFor);

/// <summary>
/// The calendar is the same indicator_releases rows the engine scores, projected
/// forward and back. Deriving it from one table is what stops the calendar and
/// the heatmap from ever disagreeing about a number.
/// </summary>
public sealed class GetCalendarHandler(AppDbContext db, ILocaleContext locale)
{
    public async Task<IReadOnlyList<CalendarEventDto>> HandleAsync(int daysBack, int daysForward, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var from = now.AddDays(-Math.Abs(daysBack));
        var to = now.AddDays(Math.Abs(daysForward));

        var rows = await db.IndicatorReleases.AsNoTracking()
            .Include(r => r.Indicator)
            .Where(r => r.ReleasedAt >= from && r.ReleasedAt <= to)
            .OrderBy(r => r.ReleasedAt)
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var indicator = r.Indicator!;
            Bias? bias = null;

            if (r.Actual is { } actual && r.ReleasedAt <= now)
            {
                var reference = r.Forecast ?? r.Previous;
                if (reference is { } baseline)
                {
                    var d = (actual - baseline) * indicator.CurrencyDirection;
                    bias = d > 0 ? Bias.Bullish : d < 0 ? Bias.Bearish : Bias.Neutral;
                }
            }

            return new CalendarEventDto(
                r.Id.ToString(),
                indicator.Code,
                indicator.FactorCode,
                indicator.Name.For(locale.Locale),
                r.CurrencyCode,
                r.ReleasedAt,
                indicator.Impact,
                r.Actual,
                r.Forecast,
                r.Previous,
                indicator.Unit,
                bias);
        }).ToList();
    }
}

public sealed class GetCalendarEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/calendar", async (
                GetCalendarHandler handler, int? daysBack, int? daysForward, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(daysBack ?? 3, daysForward ?? 7, ct)))
            .WithName("GetCalendar")
            .WithTags("Calendar")
            .Produces<IReadOnlyList<CalendarEventDto>>();
}
