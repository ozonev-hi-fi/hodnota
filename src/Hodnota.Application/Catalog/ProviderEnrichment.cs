using Hodnota.Domain.Catalog;

namespace Hodnota.Application.Catalog;

public sealed record ProviderEnrichment(
    string ProviderCode,
    IReadOnlyList<string> PlatformCodes,
    LookupOutcome Outcome,
    IReadOnlyList<ProviderLinkCandidate> Links);
