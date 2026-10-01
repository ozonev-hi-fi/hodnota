using Hodnota.Domain.Catalog;

namespace Hodnota.Application.Catalog;

// Links are candidates in the provider's preference order (e.g. a Discogs master, then its own
// release as a fallback); the caller saves the first one not already linked to another catalog
// entity. Confirmed is true only when the provider itself returned Links just now — false for an
// Outcome.NameMatch that only keeps a link the search row already had.
public sealed record ProviderEnrichment(
    string ProviderCode,
    IReadOnlyList<string> PlatformCodes,
    LookupOutcome Outcome,
    IReadOnlyList<ProviderLinkCandidate> Links,
    bool Confirmed);
