using System.Globalization;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;

namespace Hase.Operator.Presentation;

/// <summary>
/// Formats Event payloads according to their authoritative descriptor.
/// </summary>
public static class EventPayloadFormatter
{
    public static EventPayloadFormatResult Format(
        EventPayloadDescriptor? descriptor,
        object? value)
    {
        if (descriptor is null)
        {
            return value is null
                ? Result(
                    EventPayloadFormatStatus.NoPayload,
                    "No payload")
                : Result(
                    EventPayloadFormatStatus.UnexpectedPayload,
                    "Unexpected payload");
        }

        if (value is null)
        {
            return Result(
                EventPayloadFormatStatus.MissingPayload,
                "Missing payload");
        }

        return descriptor.Data switch
        {
            BooleanDataDescriptor =>
                value is bool boolean
                    ? Formatted(
                        boolean
                            ? "True"
                            : "False")
                    : TypeMismatch(),
            StringDataDescriptor =>
                value is string text
                    ? Formatted(
                        text)
                    : TypeMismatch(),
            ByteArrayDataDescriptor =>
                value is ByteArrayValue bytes
                    ? Formatted(
                        Convert.ToHexString(
                            bytes.AsSpan()))
                    : TypeMismatch(),
            NumericDataDescriptor =>
                FormatNumeric(
                    value),
            _ =>
                Result(
                    EventPayloadFormatStatus.UnsupportedDescriptor,
                    "Unsupported payload")
        };
    }

    private static EventPayloadFormatResult FormatNumeric(
        object value)
    {
        string? text =
            value switch
            {
                byte number =>
                    number.ToString(
                        CultureInfo.InvariantCulture),
                sbyte number =>
                    number.ToString(
                        CultureInfo.InvariantCulture),
                short number =>
                    number.ToString(
                        CultureInfo.InvariantCulture),
                ushort number =>
                    number.ToString(
                        CultureInfo.InvariantCulture),
                int number =>
                    number.ToString(
                        CultureInfo.InvariantCulture),
                uint number =>
                    number.ToString(
                        CultureInfo.InvariantCulture),
                long number =>
                    number.ToString(
                        CultureInfo.InvariantCulture),
                ulong number =>
                    number.ToString(
                        CultureInfo.InvariantCulture),
                float number
                    when float.IsFinite(number) =>
                        number.ToString(
                            "G9",
                            CultureInfo.InvariantCulture),
                double number
                    when double.IsFinite(number) =>
                        number.ToString(
                            "G17",
                            CultureInfo.InvariantCulture),
                decimal number =>
                    number.ToString(
                        "G29",
                        CultureInfo.InvariantCulture),
                _ =>
                    null
            };

        return text is null
            ? TypeMismatch()
            : Formatted(
                text);
    }

    private static EventPayloadFormatResult Formatted(
        string text) =>
        Result(
            EventPayloadFormatStatus.Formatted,
            text);

    private static EventPayloadFormatResult TypeMismatch() =>
        Result(
            EventPayloadFormatStatus.TypeMismatch,
            "Invalid payload");

    private static EventPayloadFormatResult Result(
        EventPayloadFormatStatus status,
        string text) =>
        new(
            status,
            text);
}
