namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103OperatingModeObservation(
    Kel103OperatingMode Mode,
    DateTimeOffset TimestampUtc);
