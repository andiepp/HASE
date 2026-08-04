namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103SetpointMutationResult(
    Kel103Setpoint Setpoint,
    decimal Value,
    Kel103OperatingMode OperatingMode,
    DateTimeOffset TimestampUtc);
