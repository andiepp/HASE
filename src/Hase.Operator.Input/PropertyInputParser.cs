using System.Globalization;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;

namespace Hase.Operator.Input;

/// <summary>
/// Converts descriptor-driven operator text into normalized typed Property
/// values without performing a Property operation.
/// </summary>
public static class PropertyInputParser
{
    public static PropertyInputParseResult Parse(
        PropertyDescriptor descriptor,
        string? input)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        if (!descriptor.AccessMode.HasFlag(
                PropertyAccessMode.Write))
        {
            return PropertyInputParseResult.Failed(
                PropertyInputFailure.PropertyNotWritable,
                "This Property is read-only.");
        }

        return descriptor.Data switch
        {
            BooleanDataDescriptor =>
                ParseBoolean(
                    input),
            NumericDataDescriptor numeric =>
                ParseNumeric(
                    numeric,
                    input),
            StringDataDescriptor =>
                ParseString(
                    input),
            ByteArrayDataDescriptor =>
                ParseByteArray(
                    input),
            _ =>
                PropertyInputParseResult.Failed(
                    PropertyInputFailure.UnsupportedDataDescriptor,
                    "This Property data type is not supported for editing.")
        };
    }

    private static PropertyInputParseResult ParseBoolean(
        string? input)
    {
        if (string.IsNullOrWhiteSpace(
                input))
        {
            return MissingInput();
        }

        if (!bool.TryParse(
                input,
                out bool value))
        {
            return PropertyInputParseResult.Failed(
                PropertyInputFailure.InvalidFormat,
                "Enter a Boolean value: true or false.");
        }

        return PropertyInputParseResult.Success(
            value);
    }

    private static PropertyInputParseResult ParseNumeric(
        NumericDataDescriptor descriptor,
        string? input)
    {
        if (string.IsNullOrWhiteSpace(
                input))
        {
            return MissingInput();
        }

        if (!double.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value)
            || !double.IsFinite(
                value))
        {
            return PropertyInputParseResult.Failed(
                PropertyInputFailure.InvalidFormat,
                "Enter a finite number using '.' as the decimal separator.");
        }

        if (descriptor.Range is not null
            && !descriptor.Range.Contains(
                value))
        {
            return PropertyInputParseResult.Failed(
                PropertyInputFailure.ValueOutsideRange,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The value must be between {descriptor.Range.Minimum} and {descriptor.Range.Maximum}."));
        }

        return PropertyInputParseResult.Success(
            value);
    }

    private static PropertyInputParseResult ParseString(
        string? input)
    {
        if (input is null)
        {
            return MissingInput();
        }

        return PropertyInputParseResult.Success(
            input);
    }

    private static PropertyInputParseResult ParseByteArray(
        string? input)
    {
        if (string.IsNullOrWhiteSpace(
                input))
        {
            return MissingInput();
        }

        if (!ByteArrayHexadecimalParser.TryParse(
                input,
                out ByteArrayValue? value))
        {
            return PropertyInputParseResult.Failed(
                PropertyInputFailure.InvalidFormat,
                "Enter complete hexadecimal bytes, for example: 00 53 FF.");
        }

        return PropertyInputParseResult.Success(
            value!);
    }

    private static PropertyInputParseResult MissingInput()
    {
        return PropertyInputParseResult.Failed(
            PropertyInputFailure.MissingInput,
            "Enter a value.");
    }
}
