using AwesomeAssertions;

using Hodnota.Application.Catalog;

namespace Hodnota.Application.Tests.Catalog;

public class CatalogKeysTests
{
    [Theory]
    [InlineData("USUM71703861", "USUM71703861")]
    [InlineData("us-um7-17-03861", "USUM71703861")]
    [InlineData(" usum71703861 ", "USUM71703861")]
    public void NormalizeIsrc_ValidIsrc_ReturnsUpperCaseWithoutSeparators(string value, string expected) =>
        CatalogKeys.NormalizeIsrc(value).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("USUM7170386")]
    [InlineData("USUM717038611")]
    [InlineData("1SUM71703861")]
    public void NormalizeIsrc_InvalidValue_ReturnsNull(string? value) =>
        CatalogKeys.NormalizeIsrc(value).Should().BeNull();

    [Fact]
    public void BarcodeVariants_TwelveDigits_ReturnsItThenItsThirteenDigitForm() =>
        CatalogKeys.BarcodeVariants("602537817016").Should().Equal("602537817016", "0602537817016");

    [Fact]
    public void BarcodeVariants_ThirteenDigitsWithLeadingZero_ReturnsItThenItsTwelveDigitForm() =>
        CatalogKeys.BarcodeVariants("0602537817016").Should().Equal("0602537817016", "602537817016");

    [Fact]
    public void BarcodeVariants_ThirteenDigitsWithoutLeadingZero_ReturnsOnlyItself() =>
        CatalogKeys.BarcodeVariants("5099902988085").Should().Equal("5099902988085");

    [Fact]
    public void BarcodeVariants_SpacesAndDashes_AreRemoved() =>
        CatalogKeys.BarcodeVariants("7 2064-24425-2 4").Should().Equal("720642442524", "0720642442524");

    [Fact]
    public void BarcodeVariants_TwelveDigitsWithLeadingZero_KeepsItsZero() =>
        CatalogKeys.BarcodeVariants("042284197928").Should().Equal("042284197928", "0042284197928");

    [Fact]
    public void BarcodeVariants_FourteenDigitsWithLeadingZeros_ReturnsItThenTheTwelveAndThirteenDigitForms() =>
        CatalogKeys.BarcodeVariants("00602577891953").Should().Equal("00602577891953", "602577891953", "0602577891953");

    [Fact]
    public void BarcodeVariants_FourteenDigitsFromAnEan13_ReturnsItThenTheThirteenDigitForm() =>
        CatalogKeys.BarcodeVariants("05099902988085").Should().Equal("05099902988085", "5099902988085");

    [Theory]
    [InlineData("602537817016", "602537817016")]
    [InlineData("0602537817016", "602537817016")]
    [InlineData("00602577891953", "602577891953")]
    [InlineData("042284197928", "042284197928")]
    [InlineData("0042284197928", "042284197928")]
    [InlineData("5099902988085", "5099902988085")]
    [InlineData("05099902988085", "5099902988085")]
    [InlineData("7 2064-24425-2 4", "720642442524")]
    public void NormalizeBarcode_AllFormsOfOneBarcode_GiveTheSameStoredForm(string value, string expected) =>
        CatalogKeys.NormalizeBarcode(value).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0000000000000")]
    [InlineData("60253781701A")]
    public void NormalizeBarcode_NotAUpcOrEan_ReturnsNull(string? value) =>
        CatalogKeys.NormalizeBarcode(value).Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("000000000000")]
    [InlineData("0000000000000")]
    [InlineData("12345678901234")]
    [InlineData("60253781701A")]
    public void BarcodeVariants_NotAUpcOrEan_ReturnsEmpty(string? value) =>
        CatalogKeys.BarcodeVariants(value).Should().BeEmpty();
}
