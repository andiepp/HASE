namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103OperatingStateSynchronizationSnapshot(
    Kel103Identity Identity,
    decimal Voltage,
    decimal Current,
    decimal Power,
    Kel103OperatingMode OperatingMode,
    bool InputEnabled,
    decimal TargetVoltage,
    decimal TargetCurrent,
    decimal TargetResistance,
    decimal TargetPower,
    DateTimeOffset TimestampUtc);
