using System.Globalization;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public sealed record Kel103MeasurementMapping
{
    private const NumberStyles NumberStyles =
        System.Globalization.NumberStyles.AllowDecimalPoint;

    private Kel103MeasurementMapping(
        Kel103Measurement measurement,
        string query,
        PropertyId propertyId,
        DescriptorPath propertyPath,
        string unitSymbol)
    {
        Measurement = measurement;
        Query = query;
        PropertyId = propertyId;
        PropertyPath = propertyPath;
        UnitSymbol = unitSymbol;
    }

    public static Kel103MeasurementMapping Voltage { get; } = new(
        Kel103Measurement.Voltage, ":MEASure:VOLTage?",
        new PropertyId("measured-voltage"), DescriptorPath.Parse("Measurement.Voltage"), "V");

    public static Kel103MeasurementMapping Current { get; } = new(
        Kel103Measurement.Current, ":MEASure:CURRent?",
        new PropertyId("measured-current"), DescriptorPath.Parse("Measurement.Current"), "A");

    public static Kel103MeasurementMapping Power { get; } = new(
        Kel103Measurement.Power, ":MEASure:POWer?",
        new PropertyId("measured-power"), DescriptorPath.Parse("Measurement.Power"), "W");

    public static IReadOnlyList<Kel103MeasurementMapping> All { get; } =
        [Voltage, Current, Power];

    public Kel103Measurement Measurement { get; }
    public string Query { get; }
    public PropertyId PropertyId { get; }
    public DescriptorPath PropertyPath { get; }
    public string UnitSymbol { get; }

    public decimal ParseResponse(string response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        if (!response.EndsWith(UnitSymbol, StringComparison.Ordinal)
            || response.Length == UnitSymbol.Length
            || !decimal.TryParse(
                response.AsSpan(0, response.Length - UnitSymbol.Length),
                NumberStyles,
                CultureInfo.InvariantCulture,
                out decimal value)
            || value < decimal.Zero)
        {
            throw new InvalidDataException(
                "The measurement response does not match the supported KEL-103 numeric format and unit.");
        }

        return value;
    }
}
