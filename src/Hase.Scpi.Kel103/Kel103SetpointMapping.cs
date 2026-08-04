using System.Globalization;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public sealed record Kel103SetpointMapping
{
    private const NumberStyles SupportedNumberStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    private Kel103SetpointMapping(
        Kel103Setpoint setpoint,
        PropertyId propertyId,
        DescriptorPath propertyPath,
        string query,
        string unitSymbol,
        Kel103OperatingMode associatedMode,
        decimal minimum,
        decimal maximum)
    {
        Setpoint = setpoint;
        PropertyId = propertyId;
        PropertyPath = propertyPath;
        Query = query;
        UnitSymbol = unitSymbol;
        AssociatedMode = associatedMode;
        Minimum = minimum;
        Maximum = maximum;
    }

    public static Kel103SetpointMapping Voltage { get; } = new(
        Kel103Setpoint.Voltage,
        new PropertyId("target-voltage"),
        DescriptorPath.Parse("Target.Voltage"),
        ":VOLTage?",
        "V",
        Kel103OperatingMode.ConstantVoltage,
        0.1m,
        120.0m);

    public static Kel103SetpointMapping Current { get; } = new(
        Kel103Setpoint.Current,
        new PropertyId("target-current"),
        DescriptorPath.Parse("Target.Current"),
        ":CURRent?",
        "A",
        Kel103OperatingMode.ConstantCurrent,
        0.0m,
        30.0m);

    public static Kel103SetpointMapping Resistance { get; } = new(
        Kel103Setpoint.Resistance,
        new PropertyId("target-resistance"),
        DescriptorPath.Parse("Target.Resistance"),
        ":RESistance?",
        "OHM",
        Kel103OperatingMode.ConstantResistance,
        0.05m,
        7500.0m);

    public static Kel103SetpointMapping Power { get; } = new(
        Kel103Setpoint.Power,
        new PropertyId("target-power"),
        DescriptorPath.Parse("Target.Power"),
        ":POWer?",
        "W",
        Kel103OperatingMode.ConstantPower,
        0.0m,
        300.0m);

    public static IReadOnlyList<Kel103SetpointMapping> All { get; } =
        [Voltage, Current, Resistance, Power];

    public Kel103Setpoint Setpoint { get; }

    public PropertyId PropertyId { get; }

    public DescriptorPath PropertyPath { get; }

    public string Query { get; }

    public string UnitSymbol { get; }

    public Kel103OperatingMode AssociatedMode { get; }

    public decimal Minimum { get; }

    public decimal Maximum { get; }

    public string FormatSetterCommand(decimal value)
    {
        if (value < Minimum || value > Maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The KEL-103 setpoint value is outside the characterized range.");
        }

        string normalizedValue = value.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);
        return Setpoint switch
        {
            Kel103Setpoint.Voltage => $":VOLTage {normalizedValue}V",
            Kel103Setpoint.Current => $":CURRent {normalizedValue}A",
            Kel103Setpoint.Resistance => $":RESistance {normalizedValue}OHM",
            Kel103Setpoint.Power => $":POWer {normalizedValue}W",
            _ => throw new InvalidOperationException(
                "The KEL-103 setpoint mapping has an unsupported target.")
        };
    }

    public decimal ParseResponse(string response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.EndsWith(UnitSymbol, StringComparison.Ordinal)
            || response.Length == UnitSymbol.Length)
        {
            throw InvalidResponse();
        }

        ReadOnlySpan<char> numberText =
            response.AsSpan(0, response.Length - UnitSymbol.Length);

        if (!decimal.TryParse(
                numberText,
                SupportedNumberStyles,
                CultureInfo.InvariantCulture,
                out decimal value)
            || value < Minimum
            || value > Maximum)
        {
            throw InvalidResponse();
        }

        return value;
    }

    private InvalidDataException InvalidResponse() =>
        new(
            $"The {Setpoint.ToString().ToLowerInvariant()} target response does not match "
            + "the supported KEL-103 format and range.");
}
