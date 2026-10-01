namespace Hodnota.Application.Catalog;

// Codes are the ISRC (tracks) or the UPC/EAN forms from CatalogKeys.BarcodeVariants (releases),
// preferred form first.
public sealed record StreamingLookupKey(StreamingResultType Type, IReadOnlyList<string> Codes);
