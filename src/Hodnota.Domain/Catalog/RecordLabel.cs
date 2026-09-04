namespace Hodnota.Domain.Catalog;

public sealed class RecordLabel : IHasTimestamps
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<Release> Releases { get; set; } = [];
}
