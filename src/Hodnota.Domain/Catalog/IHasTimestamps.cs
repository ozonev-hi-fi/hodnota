namespace Hodnota.Domain.Catalog;

public interface IHasTimestamps
{
    DateTimeOffset CreatedAtUtc { get; set; }

    DateTimeOffset UpdatedAtUtc { get; set; }
}
