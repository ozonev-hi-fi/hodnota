using Hodnota.Domain.Catalog;

namespace Hodnota.Application.Catalog;

public sealed record PlatformCheckResult(string PlatformCode, LookupOutcome Outcome);
