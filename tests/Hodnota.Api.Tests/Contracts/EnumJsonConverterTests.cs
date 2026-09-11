using System.Reflection;
using System.Text.Json.Serialization;

using AwesomeAssertions;

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
    public void Enum_HasJsonStringEnumConverterAttribute(Type enumType)
    {
        var attribute = enumType.GetCustomAttribute<JsonConverterAttribute>();

        attribute.Should().NotBeNull($"{enumType.Name} must be marked [JsonConverter(typeof(JsonStringEnumConverter<{enumType.Name}>))] so it serializes as its name, not its numeric value");
        attribute!.ConverterType.Should().Be(typeof(JsonStringEnumConverter<>).MakeGenericType(enumType));
    }
}
