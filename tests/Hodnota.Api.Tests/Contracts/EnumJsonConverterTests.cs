using System.Reflection;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Hodnota.Contracts;
using Hodnota.Contracts.Catalog;

namespace Hodnota.Api.Tests.Contracts;

public class EnumJsonConverterTests
{
    public static IEnumerable<object[]> ContractsEnumTypes() =>
        typeof(CandidateType).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.IsPublic)
            .Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(ContractsEnumTypes))]
    public void Enum_HasStrictStringEnumConverterAttribute(Type enumType)
    {
        var attribute = enumType.GetCustomAttribute<JsonConverterAttribute>();

        attribute.Should().NotBeNull($"{enumType.Name} must be marked [JsonConverter(typeof(StrictStringEnumConverter<{enumType.Name}>))] so it serializes as its name and rejects numeric values");
        attribute!.ConverterType.Should().Be(typeof(StrictStringEnumConverter<>).MakeGenericType(enumType));
    }
}
