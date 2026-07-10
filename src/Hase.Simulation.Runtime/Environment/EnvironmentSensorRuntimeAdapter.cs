using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Simulation.Instruments;

namespace Hase.Simulation.Runtime.Environment;

/// <summary>
/// Publishes values from a simulated environment sensor
/// into an existing HASE runtime instrument.
/// </summary>
public sealed class EnvironmentSensorRuntimeAdapter
{
    private readonly SimulatedEnvironmentSensor _sensor;
    private readonly RuntimeInstrument _runtimeInstrument;

    public EnvironmentSensorRuntimeAdapter(
        SimulatedEnvironmentSensor sensor,
        RuntimeInstrument runtimeInstrument)
    {
        _sensor = sensor
            ?? throw new ArgumentNullException(nameof(sensor));

        _runtimeInstrument = runtimeInstrument
            ?? throw new ArgumentNullException(
                nameof(runtimeInstrument));

        EnsureRequiredPropertyExists(
            EnvironmentSensorDescriptorFactory.TemperaturePath);

        EnsureRequiredPropertyExists(
            EnvironmentSensorDescriptorFactory.RelativeHumidityPath);

        EnsureRequiredPropertyExists(
            EnvironmentSensorDescriptorFactory.AirPressurePath);
    }

    public SimulatedEnvironmentSensor Sensor => _sensor;

    public RuntimeInstrument RuntimeInstrument =>
        _runtimeInstrument;

    /// <summary>
    /// Publishes the current sensor values to the runtime instrument.
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

        UpdateProperty(
            EnvironmentSensorDescriptorFactory.TemperaturePath,
            _sensor.Temperature,
            timestampUtc,
            quality);

        UpdateProperty(
            EnvironmentSensorDescriptorFactory.RelativeHumidityPath,
            _sensor.RelativeHumidity,
            timestampUtc,
            quality);

        UpdateProperty(
            EnvironmentSensorDescriptorFactory.AirPressurePath,
            _sensor.AirPressure,
            timestampUtc,
            quality);
    }

    private void EnsureRequiredPropertyExists(
        DescriptorPath path)
    {
        if (_runtimeInstrument.FindProperty(path) is null)
        {
            throw new ArgumentException(
                $"The runtime instrument does not contain " +
                $"the required property '{path}'.",
                nameof(_runtimeInstrument));
        }
    }

    private void UpdateProperty(
        DescriptorPath path,
        double value,
        DateTimeOffset timestampUtc,
        PropertyQuality quality)
    {
        var propertyValue = new PropertyValue(
            value,
            timestampUtc,
            quality);

        var updated =
            _runtimeInstrument.UpdatePropertyValue(
                path,
                propertyValue);

        if (!updated)
        {
            throw new InvalidOperationException(
                $"The runtime property '{path}' could not be updated.");
        }
    }
}