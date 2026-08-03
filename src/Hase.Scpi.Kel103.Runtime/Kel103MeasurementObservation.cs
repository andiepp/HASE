namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103MeasurementObservation(
    Kel103Measurement Measurement,
    decimal Value,
    DateTimeOffset TimestampUtc);
