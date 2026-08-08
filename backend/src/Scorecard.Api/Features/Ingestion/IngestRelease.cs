using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Domain;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Shared;

namespace Scorecard.Api.Features.Ingestion;

public sealed record IngestReleaseRequest(
    string IndicatorCode,
    string CurrencyCode,
    DateOnly Period,
    decimal? Actual,
    decimal? Forecast,
    decimal? Previous,
    int Revision,
    DateTimeOffset ReleasedAt,
    DataSource Source,
    string? SourceRef);

public sealed class IngestReleaseValidator : AbstractValidator<IngestReleaseRequest>
{
    public IngestReleaseValidator()
    {
        RuleFor(x => x.IndicatorCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReleasedAt).NotEqual(default(DateTimeOffset));
    }
}

public sealed class IngestReleaseHandler(AppDbContext db)
{
    /// <summary>
    /// Idempotent on the natural key (indicator, currency, period, revision).
    /// Re-posting the same print updates it in place; a restatement arrives as a
    /// new revision and supersedes without destroying the original.
    /// </summary>
    public async Task<(bool Created, string? Error)> HandleAsync(IngestReleaseRequest request, CancellationToken ct)
    {
        var indicator = await db.Indicators
            .FirstOrDefaultAsync(i => i.Code == request.IndicatorCode, ct);

        if (indicator is null) return (false, $"Unknown indicator '{request.IndicatorCode}'.");

        var currency = request.CurrencyCode.ToUpperInvariant();

        var existing = await db.IndicatorReleases.FirstOrDefaultAsync(r =>
            r.IndicatorId == indicator.Id &&
            r.CurrencyCode == currency &&
            r.Period == request.Period &&
            r.Revision == request.Revision, ct);

        if (existing is not null)
        {
            existing.Actual = request.Actual;
            existing.Forecast = request.Forecast;
            existing.Previous = request.Previous;
            existing.ReleasedAt = request.ReleasedAt;
            existing.Source = request.Source;
            existing.SourceRef = request.SourceRef;
            existing.ImportedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return (false, null);
        }

        db.IndicatorReleases.Add(new IndicatorRelease
        {
            Id = Guid.NewGuid(),
            IndicatorId = indicator.Id,
            CurrencyCode = currency,
            Period = request.Period,
            Actual = request.Actual,
            Forecast = request.Forecast,
            Previous = request.Previous,
            Revision = request.Revision,
            ReleasedAt = request.ReleasedAt,
            Source = request.Source,
            SourceRef = request.SourceRef,
            ImportedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return (true, null);
    }
}

public sealed class IngestReleaseEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/admin/releases", async (
            IngestReleaseRequest request,
            IngestReleaseHandler handler,
            IValidator<IngestReleaseRequest> validator,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var (created, error) = await handler.HandleAsync(request, ct);
            if (error is not null) return Results.BadRequest(new { error });

            return created ? Results.Created() : Results.Ok();
        })
        .WithName("IngestRelease")
        .WithTags("Admin");
}
