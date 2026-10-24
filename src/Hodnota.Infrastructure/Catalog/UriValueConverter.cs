using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hodnota.Infrastructure.Catalog;

// AbsoluteUri, not ToString(): ToString() unescapes percent-encoded reserved characters (e.g. %23,
// %3D), corrupting stored URLs that have them (CDN cover art URLs in particular).
public sealed class UriValueConverter() : ValueConverter<Uri, string>(
    v => v.AbsoluteUri,
    v => new Uri(v, UriKind.Absolute));
