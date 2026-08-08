using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Database;

/// <summary>
/// Maps <see cref="LocalizedText"/> to a single jsonb column.
///
/// A converter rather than <c>ToJson()</c> owned entities: the shape is fixed at
/// two keys, nothing queries inside it (handlers project to one locale), and a
/// converter keeps the generated migration trivially readable.
/// </summary>
public static class LocalizedTextConversion
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static readonly ValueConverter<LocalizedText, string> Converter = new(
        v => JsonSerializer.Serialize(v, Options),
        v => JsonSerializer.Deserialize<LocalizedText>(v, Options) ?? new LocalizedText());

    /// <summary>
    /// Required because LocalizedText is a mutable reference type — without an
    /// explicit comparer EF cannot detect in-place edits.
    /// </summary>
    public static readonly ValueComparer<LocalizedText> Comparer = new(
        (a, b) => a != null && b != null && a.Mn == b.Mn && a.En == b.En,
        v => HashCode.Combine(v.Mn, v.En),
        v => new LocalizedText(v.Mn, v.En));
}
