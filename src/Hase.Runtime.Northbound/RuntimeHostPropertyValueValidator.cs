using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Performs safe host-side validation where the immutable Property descriptor
/// model can determine validity without endpoint-specific knowledge.
/// </summary>
internal static class RuntimeHostPropertyValueValidator
{
    public static bool IsValid(
        PropertyDescriptor descriptor,
        object? requestedValue)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        return descriptor.Data switch
        {
            BooleanDataDescriptor =>
                requestedValue is bool,

            StringDataDescriptor =>
                requestedValue is string,

            NumericDataDescriptor numericDescriptor =>
                IsValidNumericValue(
                    numericDescriptor,
                    requestedValue),

            _ =>
                true
        };
    }

    private static bool IsValidNumericValue(
        NumericDataDescriptor descriptor,
        object? requestedValue)
    {
        if (!TryConvertNumericValue(
                requestedValue,
                out double numericValue))
        {
            return false;
        }

        return descriptor.Range?.Contains(
                numericValue)
            ?? true;
    }

    private static bool TryConvertNumericValue(
        object? requestedValue,
        out double numericValue)
    {
        switch (requestedValue)
        {
            case byte value:
                numericValue = value;
                return true;

            case sbyte value:
                numericValue = value;
                return true;

            case short value:
                numericValue = value;
                return true;

            case ushort value:
                numericValue = value;
                return true;

            case int value:
                numericValue = value;
                return true;

            case uint value:
                numericValue = value;
                return true;

            case long value:
                numericValue = value;
                return true;

            case ulong value:
                numericValue = value;
                return true;

            case float value:
                numericValue = value;
                return true;

            case double value:
                numericValue = value;
                return true;

            case decimal value:
                numericValue =
                    Convert.ToDouble(
                        value);

                return true;

            default:
                numericValue =
                    default;

                return false;
        }
    }
}