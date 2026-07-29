using System.Globalization;
using Hase.Core.Domain.Data;

namespace Hase.Operator.Input;

internal static class DescriptorInputParser
{
    public static DescriptorInputParseResult Parse(
        DataDescriptor descriptor,
        string? input)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        return descriptor switch
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
                DescriptorInputParseResult.Failed(
                    DescriptorInputFailure.UnsupportedDataDescriptor,
                    "This data type is not supported for editing.")
        };
    }

    private static DescriptorInputParseResult ParseBoolean(
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
            return DescriptorInputParseResult.Failed(
                DescriptorInputFailure.InvalidFormat,
                "Enter a Boolean value: true or false.");
        }

        return DescriptorInputParseResult.Success(
            value);
    }

    private static DescriptorInputParseResult ParseNumeric(
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
            return DescriptorInputParseResult.Failed(
                DescriptorInputFailure.InvalidFormat,
                "Enter a finite number using '.' as the decimal separator.");
        }

        if (descriptor.Range is not null
            && !descriptor.Range.Contains(
                value))
        {
            return DescriptorInputParseResult.Failed(
                DescriptorInputFailure.ValueOutsideRange,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The value must be between {descriptor.Range.Minimum} and {descriptor.Range.Maximum}."));
        }

        return DescriptorInputParseResult.Success(
            value);
    }

    private static DescriptorInputParseResult ParseString(
        string? input)
    {
        if (input is null)
        {
            return MissingInput();
        }

        return DescriptorInputParseResult.Success(
            input);
    }

    private static DescriptorInputParseResult ParseByteArray(
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
            return DescriptorInputParseResult.Failed(
                DescriptorInputFailure.InvalidFormat,
                "Enter complete hexadecimal bytes, for example: 00 53 FF.");
        }

        return DescriptorInputParseResult.Success(
            value!);
    }

    private static DescriptorInputParseResult MissingInput()
    {
        return DescriptorInputParseResult.Failed(
            DescriptorInputFailure.MissingInput,
            "Enter a value.");
    }
}
