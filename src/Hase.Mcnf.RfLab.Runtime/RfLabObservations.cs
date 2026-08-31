namespace Hase.Mcnf.RfLab.Runtime;

public sealed record RfLabSensorObservation(
    int AdcValue,
    double Millivolts,
    double Level,
    DateTimeOffset TimestampUtc);

public sealed record RfLabIndicatorObservation(
    bool Enabled,
    DateTimeOffset TimestampUtc);

public sealed record RfLabConfigurationObservation(
    RfLabConfiguration Configuration,
    DateTimeOffset TimestampUtc);

public sealed record RfLabSynchronizationSnapshot(
    RfLabIdentity Identity,
    RfLabConfiguration Configuration,
    RfLabSensorObservation Sensor,
    DateTimeOffset TimestampUtc);
