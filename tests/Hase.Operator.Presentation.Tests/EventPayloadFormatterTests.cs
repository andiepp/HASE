using System.Globalization;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;

namespace Hase.Operator.Presentation.Tests;

public sealed class EventPayloadFormatterTests
{
    [Fact]
    public void Format_WithoutDescriptorOrValue_ShouldReportNoPayload()
    {
        AssertResult(
            EventPayloadFormatter.Format(
                descriptor: null,
                value: null),
            EventPayloadFormatStatus.NoPayload,
            "No payload");
    }

    [Fact]
    public void Format_WithDescriptorButNoValue_ShouldReportMissingPayload()
    {
        AssertResult(
            EventPayloadFormatter.Format(
                CreatePayload(
                    new BooleanDataDescriptor()),
                value: null),
            EventPayloadFormatStatus.MissingPayload,
            "Missing payload");
    }

    [Fact]
    public void Format_WithoutDescriptorButWithValue_ShouldReportUnexpectedPayload()
    {
        AssertResult(
            EventPayloadFormatter.Format(
                descriptor: null,
                value: true),
            EventPayloadFormatStatus.UnexpectedPayload,
            "Unexpected payload");
    }

    [Theory]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    public void Format_Boolean_ShouldUseStableText(
        bool value,
        string expected)
    {
        AssertFormatted(
            new BooleanDataDescriptor(),
            value,
            expected);
    }

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(-12.5, "-12.5")]
    [InlineData(1.25, "1.25")]
    public void Format_Double_ShouldUseRoundTripSafeInvariantText(
        double value,
        string expected)
    {
        AssertFormatted(
            CreateNumericDescriptor(),
            value,
            expected);
    }

    [Fact]
    public void Format_Decimal_ShouldUseInvariantText()
    {
        CultureInfo originalCulture =
            CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo(
                    "de-DE");

            AssertFormatted(
                CreateNumericDescriptor(),
                12.5m,
                "12.5");
        }
        finally
        {
            CultureInfo.CurrentCulture =
                originalCulture;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Event text")]
    public void Format_String_ShouldPreserveExactText(
        string value)
    {
        AssertFormatted(
            new StringDataDescriptor(),
            value,
            value);
    }

    [Fact]
    public void Format_EmptyByteArray_ShouldProduceEmptyText()
    {
        AssertFormatted(
            new ByteArrayDataDescriptor(),
            new ByteArrayValue(
                Array.Empty<byte>()),
            string.Empty);
    }

    [Fact]
    public void Format_ByteArray_ShouldUseUppercaseHexadecimal()
    {
        byte[] source =
        [
            0x00,
            0x53,
            0xFF
        ];
        ByteArrayValue value =
            new(
                source);

        AssertFormatted(
            new ByteArrayDataDescriptor(),
            value,
            "0053FF");

        source[0] =
            0xAA;

        Assert.Equal(
            new byte[]
            {
                0x00,
                0x53,
                0xFF
            },
            value.ToArray());
    }

    [Theory]
    [MemberData(nameof(MismatchedValues))]
    public void Format_MismatchedValue_ShouldReportInvalidPayload(
        DataDescriptor descriptor,
        object value)
    {
        AssertResult(
            EventPayloadFormatter.Format(
                CreatePayload(
                    descriptor),
                value),
            EventPayloadFormatStatus.TypeMismatch,
            "Invalid payload");
    }

    public static TheoryData<DataDescriptor, object> MismatchedValues =>
        new()
        {
            {
                new BooleanDataDescriptor(),
                "true"
            },
            {
                new StringDataDescriptor(),
                true
            },
            {
                new ByteArrayDataDescriptor(),
                new byte[]
                {
                    0x01
                }
            },
            {
                CreateNumericDescriptor(),
                "12.5"
            }
        };

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Format_NonFiniteNumeric_ShouldReportInvalidPayload(
        double value)
    {
        AssertResult(
            EventPayloadFormatter.Format(
                CreatePayload(
                    CreateNumericDescriptor()),
                value),
            EventPayloadFormatStatus.TypeMismatch,
            "Invalid payload");
    }

    [Fact]
    public void Format_UnknownDescriptor_ShouldReportUnsupportedPayload()
    {
        AssertResult(
            EventPayloadFormatter.Format(
                CreatePayload(
                    new TestDataDescriptor()),
                new object()),
            EventPayloadFormatStatus.UnsupportedDescriptor,
            "Unsupported payload");
    }

    private static void AssertFormatted(
        DataDescriptor descriptor,
        object value,
        string expected)
    {
        AssertResult(
            EventPayloadFormatter.Format(
                CreatePayload(
                    descriptor),
                value),
            EventPayloadFormatStatus.Formatted,
            expected);
    }

    private static void AssertResult(
        EventPayloadFormatResult result,
        EventPayloadFormatStatus expectedStatus,
        string expectedText)
    {
        Assert.Equal(
            expectedStatus,
            result.Status);
        Assert.Equal(
            expectedText,
            result.Text);
    }

    private static EventPayloadDescriptor CreatePayload(
        DataDescriptor descriptor) =>
        new(
            "Payload",
            descriptor);

    private static NumericDataDescriptor CreateNumericDescriptor()
    {
        Quantity quantity =
            new(
                "ratio",
                "Ratio");
        Unit unit =
            new(
                "ratio",
                "Ratio",
                "ratio",
                quantity);

        return new NumericDataDescriptor(
            quantity,
            unit);
    }

    private sealed record TestDataDescriptor
        : DataDescriptor;
}
