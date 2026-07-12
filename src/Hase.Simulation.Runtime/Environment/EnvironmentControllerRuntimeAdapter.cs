using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Simulation.Environment;

namespace Hase.Simulation.Runtime.Environment;

/// <summary>
/// Publishes the authoritative state of a simulated environment
/// controller into an existing HASE runtime instrument.
/// </summary>
public sealed class EnvironmentControllerRuntimeAdapter
{
    private readonly EnvironmentControllerSimulation _simulation;
    private readonly RuntimeInstrument _runtimeInstrument;

    public EnvironmentControllerRuntimeAdapter(
        EnvironmentControllerSimulation simulation,
        RuntimeInstrument runtimeInstrument)
    {
        _simulation = simulation
            ?? throw new ArgumentNullException(nameof(simulation));

        _runtimeInstrument = runtimeInstrument
            ?? throw new ArgumentNullException(
                nameof(runtimeInstrument));

        EnsureRequiredPropertyExists();
    }

    public EnvironmentControllerSimulation Simulation =>
        _simulation;

    public RuntimeInstrument RuntimeInstrument =>
        _runtimeInstrument;

    /// <summary>
    /// Publishes the current authoritative controller state
    /// into the runtime cache.
    /// </summary>
    public void Publish(
        DateTimeOffset timestampUtc,
        PropertyQuality quality = PropertyQuality.Good)
    {
        if (timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be expressed in UTC.",
                nameof(timestampUtc));
        }

        var propertyValue =
            new PropertyValue(
                _simulation.State.TargetTemperature,
                timestampUtc,
                quality);

        var updated =
            _runtimeInstrument.UpdatePropertyValue(
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePath,
                propertyValue);

        if (!updated)
        {
            throw new InvalidOperationException(
                "The target-temperature runtime property " +
                "could not be updated.");
        }
    }

    private void EnsureRequiredPropertyExists()
    {
        var property =
            _runtimeInstrument.FindProperty(
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePath);

        if (property is null)
        {
            throw new ArgumentException(
                "The runtime instrument does not contain " +
                "the required target-temperature property.",
                nameof(_runtimeInstrument));
        }
    }
}
