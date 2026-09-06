namespace Hodnota.Application.Catalog;

// PlatformCode matches Hodnota.Infrastructure.Catalog.PlatformCodes — Application only passes it
// through, it never needs to know the concrete values.
public sealed record ProviderLinkCandidate(string PlatformCode, string ExternalId, Uri ExternalUrl);
