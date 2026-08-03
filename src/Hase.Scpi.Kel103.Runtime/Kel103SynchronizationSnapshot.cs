namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103SynchronizationSnapshot(
    Kel103Identity Identity,
    decimal Voltage,
    decimal Current,
    decimal Power,
    DateTimeOffset TimestampUtc);
