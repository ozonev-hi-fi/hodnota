using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

public sealed class Platform : IHasTimestamps
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    [Description("Unique slug identifying this platform, e.g. \"youtube-music\" — see Hodnota.Infrastructure.Catalog.PlatformCodes.")]
    public required string Code { get; set; }

    public required PlatformType Type { get; set; }

    public Uri? WebsiteUrl { get; set; }

    public Uri? IconUrl { get; set; }

    [Description("Soft-disable flag: false stops new ProviderLinks from being created for this platform, without deleting existing links/history.")]
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<ProviderLink> ProviderLinks { get; set; } = [];
}
