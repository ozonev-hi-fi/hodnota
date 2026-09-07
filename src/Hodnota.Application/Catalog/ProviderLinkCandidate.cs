namespace Hodnota.Application.Catalog;

// PlatformCode matches Hodnota.Infrastructure.Catalog.PlatformCodes
public sealed record ProviderLinkCandidate(string PlatformCode, string ExternalId, Uri ExternalUrl);
