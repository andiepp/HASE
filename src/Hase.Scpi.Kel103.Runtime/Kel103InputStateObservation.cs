namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103InputStateObservation(
    bool InputEnabled,
    DateTimeOffset TimestampUtc);
