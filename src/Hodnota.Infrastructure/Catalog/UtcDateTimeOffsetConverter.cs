using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hodnota.Infrastructure.Catalog;

public sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
    v => v.ToUniversalTime(),
    v => v);
