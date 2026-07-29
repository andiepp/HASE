using System.Globalization;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Operator.Input.Tests;

public sealed class PropertyInputParserTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    [InlineData(" True ", true)]
    public void Parse_BooleanCanonicalInput_ShouldReturnBoolean(
        string input,
        bool expected)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    new BooleanDataDescriptor()),
                input);

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            expected,
            Assert.IsType<bool>(
                result.Value));
    }

    [Theory]
    [InlineData(null, PropertyInputFailure.MissingInput)]
    [InlineData("", PropertyInputFailure.MissingInput)]
    [InlineData("1", PropertyInputFailure.InvalidFormat)]
    [InlineData("yes", PropertyInputFailure.InvalidFormat)]
    public void Parse_InvalidBoolean_ShouldReturnStableFailure(
        string? input,
        PropertyInputFailure expected)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    new BooleanDataDescriptor()),
                input);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            expected,
            result.Failure);
        Assert.Null(
            result.Value);
        Assert.NotEmpty(
            result.Message);
    }

    [Theory]
    [InlineData("23.5", 23.5)]
    [InlineData("-1.25e2", -125.0)]
    [InlineData(" 7 ", 7.0)]
    public void Parse_InvariantFiniteNumericInput_ShouldReturnDouble(
        string input,
        double expected)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    CreateNumericDescriptor()),
                input);

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            expected,
            Assert.IsType<double>(
                result.Value));
    }

    [Theory]
    [InlineData(null, PropertyInputFailure.MissingInput)]
    [InlineData("", PropertyInputFailure.MissingInput)]
    [InlineData("23,5", PropertyInputFailure.InvalidFormat)]
    [InlineData("NaN", PropertyInputFailure.InvalidFormat)]
    [InlineData("Infinity", PropertyInputFailure.InvalidFormat)]
    [InlineData("1,000", PropertyInputFailure.InvalidFormat)]
    public void Parse_InvalidNumericInput_ShouldReturnStableFailure(
        string? input,
        PropertyInputFailure expected)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    CreateNumericDescriptor()),
                input);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            expected,
            result.Failure);
    }

    [Theory]
    [InlineData(-10.0)]
    [InlineData(10.0)]
    public void Parse_NumericRangeBoundary_ShouldBeInclusive(
        double value)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    CreateNumericDescriptor(
                        new ValueRange(
                            -10,
                            10))),
                value.ToString(
                    CultureInfo.InvariantCulture));

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            value,
            result.Value);
    }

    [Theory]
    [InlineData(-10.01)]
    [InlineData(10.01)]
    public void Parse_NumericOutsideDescriptorRange_ShouldReturnRangeFailure(
        double value)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    CreateNumericDescriptor(
                        new ValueRange(
                            -10,
                            10))),
                value.ToString(
                    CultureInfo.InvariantCulture));

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            PropertyInputFailure.ValueOutsideRange,
            result.Failure);
        Assert.Equal(
            "The value must be between -10 and 10.",
            result.Message);
    }

    [Fact]
    public void Parse_UnderCommaDecimalCulture_ShouldRemainInvariant()
    {
        CultureInfo originalCulture =
            CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo(
                    "de-DE");

            Assert.True(
                PropertyInputParser.Parse(
                    CreateDescriptor(
                        CreateNumericDescriptor()),
                    "23.5").IsSuccess);
            Assert.False(
                PropertyInputParser.Parse(
                    CreateDescriptor(
                        CreateNumericDescriptor()),
                    "23,5").IsSuccess);
        }
        finally
        {
            CultureInfo.CurrentCulture =
                originalCulture;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  exact text  ")]
    [InlineData("\"quoted\"")]
    public void Parse_String_ShouldPreserveExactInput(
        string input)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    new StringDataDescriptor()),
                input);

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            input,
            Assert.IsType<string>(
                result.Value));
    }

    [Theory]
    [InlineData("00 53 FF")]
    [InlineData("0053ff")]
    [InlineData("0 0 5 3 F F")]
    public void Parse_ByteArrayEstablishedAdr0036Syntax_ShouldPreserveBytes(
        string input)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    new ByteArrayDataDescriptor()),
                input);

        Assert.True(
            result.IsSuccess);
        Assert.Equal(
            new byte[]
            {
                0x00,
                0x53,
                0xFF
            },
            Assert.IsType<ByteArrayValue>(
                result.Value).ToArray());
    }

    [Theory]
    [InlineData(null, PropertyInputFailure.MissingInput)]
    [InlineData("", PropertyInputFailure.MissingInput)]
    [InlineData("F", PropertyInputFailure.InvalidFormat)]
    [InlineData("00 GG", PropertyInputFailure.InvalidFormat)]
    [InlineData("00 53 FFF", PropertyInputFailure.InvalidFormat)]
    public void Parse_InvalidByteArray_ShouldReturnStableFailure(
        string? input,
        PropertyInputFailure expected)
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    new ByteArrayDataDescriptor()),
                input);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            expected,
            result.Failure);
    }

    [Fact]
    public void Parse_ReadOnlyProperty_ShouldRejectBeforeParsing()
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                new BooleanDataDescriptor(),
                PropertyAccessMode.Read);

        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                descriptor,
                "not-a-boolean");

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            PropertyInputFailure.PropertyNotWritable,
            result.Failure);
        Assert.Equal(
            "This Property is read-only.",
            result.Message);
    }

    [Fact]
    public void Parse_UnknownDescriptor_ShouldReturnUnsupportedFailure()
    {
        PropertyInputParseResult result =
            PropertyInputParser.Parse(
                CreateDescriptor(
                    new UnsupportedDataDescriptor()),
                "value");

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            PropertyInputFailure.UnsupportedDataDescriptor,
            result.Failure);
    }

    [Fact]
    public void Parse_NullDescriptor_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                PropertyInputParser.Parse(
                    null!,
                    "value"));
    }

    private static PropertyDescriptor CreateDescriptor(
        DataDescriptor data,
        PropertyAccessMode accessMode =
            PropertyAccessMode.ReadWrite)
    {
        return new PropertyDescriptor(
            new PropertyId(
                "property-01"),
            new DescriptorPath(
                "Property",
                "Value"),
            "Property",
            data)
        {
            AccessMode =
                accessMode
        };
    }

    private static NumericDataDescriptor CreateNumericDescriptor(
        ValueRange? range = null)
    {
        return new NumericDataDescriptor(
            Quantities.Temperature,
            Units.Celsius,
            range);
    }

    private sealed record UnsupportedDataDescriptor
        : DataDescriptor;
}
