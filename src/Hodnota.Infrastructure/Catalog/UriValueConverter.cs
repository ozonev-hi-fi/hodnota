using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hodnota.Infrastructure.Catalog;

public sealed class UriValueConverter() : ValueConverter<Uri, string>(
    v => v.ToString(),
    v => new Uri(v, UriKind.Absolute));
