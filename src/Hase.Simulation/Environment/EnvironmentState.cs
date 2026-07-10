namespace Hase.Simulation.Environment;

/// <summary>
/// Represents the physical state of a simulated environment.
/// </summary>
public sealed record EnvironmentState(
    double Temperature,
    double RelativeHumidity,
    double AirPressure);