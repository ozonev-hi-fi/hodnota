using System.Text.Json.Serialization;

namespace Hodnota.Contracts;

// JsonStringEnumConverter<T> accepts integer values by default, so {"type":7} would bind to an
// undefined enum value instead of failing model binding.
public sealed class StrictStringEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false)
    where TEnum : struct, Enum;
