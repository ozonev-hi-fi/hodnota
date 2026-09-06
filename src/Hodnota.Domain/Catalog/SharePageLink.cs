using System.ComponentModel;

namespace Hodnota.Domain.Catalog;

public sealed class SharePageLink
{
    public Guid Id { get; set; }

    public Guid SharePageId { get; set; }

    public Guid ProviderLinkId { get; set; }

    [Description("Display order among this SharePage's links, ascending.")]
    public int DisplayOrder { get; set; }

    [Description("Whether this link is shown on the page; false lets the owner exclude a link the catalog otherwise has.")]
    public bool IsVisible { get; set; } = true;

    public SharePage SharePage { get; set; } = null!;

    public ProviderLink ProviderLink { get; set; } = null!;
}
