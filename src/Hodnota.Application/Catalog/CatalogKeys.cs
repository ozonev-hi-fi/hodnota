using System.Text.RegularExpressions;

namespace Hodnota.Application.Catalog;

public static partial class CatalogKeys
{
    public static string? NormalizeIsrc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var compact = value.Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        return IsrcPattern().IsMatch(compact) ? compact : null;
    }

    // The one stored form of a barcode: 12 digits (UPC-A) when it fits, else 13 (EAN-13). Providers
    // write the same barcode with different leading zeros (Tidal returns 14 digits). Null when the
    // value is not a UPC/EAN.
    public static string? NormalizeBarcode(string? value) => BarcodeVariants(value) is [var first, ..] ? Canonical(first) : null;

    // The barcode as given, then its 12- and 13-digit forms. Providers do not normalize barcodes, so
    // a lookup may need to try each form. Empty when the value is not a UPC/EAN.
    public static IReadOnlyList<string> BarcodeVariants(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !BarcodeCharactersPattern().IsMatch(value))
        {
            return [];
        }

        var digits = new string([.. value.Where(char.IsAsciiDigit)]);
        var significant = digits.TrimStart('0');
        if (digits.Length is < 12 or > 14 || significant.Length is 0 or > 13)
        {
            return [];
        }

        List<string> forms = [digits];
        if (significant.Length <= 12)
        {
            forms.Add(significant.PadLeft(12, '0'));
            forms.Add(significant.PadLeft(13, '0'));
        }
        else
        {
            forms.Add(significant);
        }

        return [.. forms.Distinct()];
    }

    private static string Canonical(string digits)
    {
        var significant = digits.TrimStart('0');
        return significant.Length <= 12 ? significant.PadLeft(12, '0') : significant;
    }

    [GeneratedRegex("^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$")]
    private static partial Regex IsrcPattern();

    [GeneratedRegex(@"^[0-9\s-]+$")]
    private static partial Regex BarcodeCharactersPattern();
}
