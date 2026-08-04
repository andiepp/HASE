using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public static class Kel103OperatingModeMapping
{
    public static PropertyId PropertyId { get; } = new("operating-mode");

    public static DescriptorPath PropertyPath { get; } =
        DescriptorPath.Parse("Operating.Mode");

    public static string Query { get; } = ":FUNCtion?";

    public static Kel103OperatingMode ParseResponse(string response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response switch
        {
            "CC" => Kel103OperatingMode.ConstantCurrent,
            "CV" => Kel103OperatingMode.ConstantVoltage,
            "CR" => Kel103OperatingMode.ConstantResistance,
            "CW" => Kel103OperatingMode.ConstantPower,
            "SHORt" => Kel103OperatingMode.ShortCircuit,
            _ => throw new InvalidDataException(
                "The operating-mode response does not match the supported KEL-103 format.")
        };
    }

    public static string ToNormalizedValue(Kel103OperatingMode mode) =>
        mode switch
        {
            Kel103OperatingMode.ConstantCurrent => "CC",
            Kel103OperatingMode.ConstantVoltage => "CV",
            Kel103OperatingMode.ConstantResistance => "CR",
            Kel103OperatingMode.ConstantPower => "CW",
            Kel103OperatingMode.ShortCircuit => "SHORT",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
}
