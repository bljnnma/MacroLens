using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database;

/// <summary>
/// Maps the per-currency readings of a factor to one jsonb column.
///
/// A column rather than a child table: the list is one or two entries, it is
/// always read with its parent factor row, and nothing ever queries inside it.
/// A table would add a third join to the asset detail page to store what is
/// effectively a snapshot field.
/// </summary>
public static class FactorReadingConversion
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static readonly ValueConverter<List<FactorReading>, string> Converter = new(
        v => JsonSerializer.Serialize(v, Options),
        v => JsonSerializer.Deserialize<List<FactorReading>>(v, Options) ?? new List<FactorReading>());

    /// <summary>
    /// Required for a mutable collection: without it EF cannot tell that the list
    /// was replaced. The snapshot clones so a later edit to the tracked instance
    /// cannot rewrite what was read.
    /// </summary>
    public static readonly ValueComparer<List<FactorReading>> Comparer = new(
        (a, b) => a != null && b != null && a.SequenceEqual(b),
        v => v.Aggregate(0, (hash, r) => HashCode.Combine(hash, r.GetHashCode())),
        v => v.ToList());
}
