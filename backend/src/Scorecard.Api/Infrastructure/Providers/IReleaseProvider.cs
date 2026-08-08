using Scorecard.Api.Domain;

namespace Scorecard.Api.Infrastructure.Providers;

/// <summary>One dated reading, provider-agnostic.</summary>
public sealed record ProviderObservation(DateOnly Date, decimal Value);

/// <summary>
/// A statistical source the ingestion slice can read a series from.
///
/// The abstraction is deliberately thin — one method — because that is the
/// entire surface the four providers share. FRED, BIS, Eurostat and the OECD
/// disagree about formats, key encodings and which statistics they even carry;
/// anything richer here would be a lowest common denominator that fits none of
/// them. Everything provider-specific stays inside its own client.
/// </summary>
public interface IReleaseProvider
{
    DataSource Provider { get; }

    /// <summary>False when the provider needs credentials it has not been given.</summary>
    bool IsConfigured { get; }

    /// <param name="seriesId">
    /// The provider's own key. Each implementation documents its encoding —
    /// see the class remarks.
    /// </param>
    Task<IReadOnlyList<ProviderObservation>> GetObservationsAsync(
        string seriesId, DateOnly from, CancellationToken ct = default);
}

/// <summary>Resolves a provider by its enum value, so mapping rows stay data.</summary>
public sealed class ReleaseProviderRegistry(IEnumerable<IReleaseProvider> providers)
{
    private readonly IReadOnlyDictionary<DataSource, IReleaseProvider> _providers =
        providers.ToDictionary(p => p.Provider);

    public IReleaseProvider Resolve(DataSource source) =>
        _providers.TryGetValue(source, out var provider)
            ? provider
            : throw new InvalidOperationException($"No release provider registered for {source}.");

    public bool IsConfigured(DataSource source) =>
        _providers.TryGetValue(source, out var provider) && provider.IsConfigured;
}
