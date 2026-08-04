namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103SetpointObservation(
    Kel103Setpoint Setpoint,
    decimal Value,
    DateTimeOffset TimestampUtc);
