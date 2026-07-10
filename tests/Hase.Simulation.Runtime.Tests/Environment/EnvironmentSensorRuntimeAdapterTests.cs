using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Simulation.Environment;
using Hase.Simulation.Instruments;
using Hase.Simulation.Runtime.Environment;
using Hase.Simulation.Values;
using Hase.Simulation.Values.Periodic;

namespace Hase.Simulation.Runtime.Tests.Environment;

public sealed class EnvironmentSensorRuntimeAdapterTests
{
    private static readonly DateTimeOffset TestTimestamp =
        new(
            year: 2026,
            month: 7,
            day: 10,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

    [Fact]
    public void Constructor_NullSensor_Throws()
    {
        var runtimeInstrument =
            CreateRuntimeInstrument();

        Assert.Throws<ArgumentNullException>(
            () => new EnvironmentSensorRuntimeAdapter(
                null!,
                runtimeInstrument));
    }

    [Fact]
    public void Constructor_NullRuntimeInstrument_Throws()
    {
        var sensor =
            CreateConstantSensor(
                temperature: 20.0,
                relativeHumidity: 50.0,
                airPressure: 1013.0);

        Assert.Throws<ArgumentNullException>(
            () => new EnvironmentSensorRuntimeAdapter(
                sensor,
                null!));
    }

    [Fact]
    public void Constructor_IncompatibleInstrument_Throws()
    {
        var sensor =
            CreateConstantSensor(
                temperature: 20.0,
                relativeHumidity: 50.0,
                airPressure: 1013.0);

        var context = new RuntimeContext();

        var endpoint = context.AddEndpoint(
            new EndpointDescriptor(
                new EndpointId("simulation-endpoint")));

        var incompatibleInstrument =
            new RuntimeInstrument(
                endpoint,
                new Hase.Core.Domain.Instruments.InstrumentDescriptor(
                    new InstrumentId("incompatible"),
                    "Incompatible",
                    new Hase.Core.Domain.Instruments.InstrumentKind(
                        "test")));

        Assert.Throws<ArgumentException>(
            () => new EnvironmentSensorRuntimeAdapter(
                sensor,
                incompatibleInstrument));
    }

    [Fact]
    public void Publish_WritesAllSensorValues()
    {
        var sensor =
            CreateConstantSensor(
                temperature: 21.5,
                relativeHumidity: 58.0,
                airPressure: 1012.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var adapter =
            new EnvironmentSensorRuntimeAdapter(
                sensor,
                runtimeInstrument);

        adapter.Publish(TestTimestamp);

        AssertPropertyValue(
            runtimeInstrument,
            EnvironmentSensorDescriptorFactory.TemperaturePath,
            expectedValue: 21.5,
            expectedTimestamp: TestTimestamp,
            expectedQuality: PropertyQuality.Good);

        AssertPropertyValue(
            runtimeInstrument,
            EnvironmentSensorDescriptorFactory.RelativeHumidityPath,
            expectedValue: 58.0,
            expectedTimestamp: TestTimestamp,
            expectedQuality: PropertyQuality.Good);

        AssertPropertyValue(
            runtimeInstrument,
            EnvironmentSensorDescriptorFactory.AirPressurePath,
            expectedValue: 1012.5,
            expectedTimestamp: TestTimestamp,
            expectedQuality: PropertyQuality.Good);
    }

    [Fact]
    public void Publish_UsesSpecifiedQuality()
    {
        var sensor =
            CreateConstantSensor(
                temperature: 21.5,
                relativeHumidity: 58.0,
                airPressure: 1012.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var adapter =
            new EnvironmentSensorRuntimeAdapter(
                sensor,
                runtimeInstrument);

        adapter.Publish(
            TestTimestamp,
            PropertyQuality.Uncertain);

        Assert.All(
            runtimeInstrument.Properties,
            property =>
            {
                Assert.NotNull(property.CurrentValue);

                Assert.Equal(
                    PropertyQuality.Uncertain,
                    property.CurrentValue!.Quality);
            });
    }

    [Fact]
    public void Publish_AfterEnvironmentUpdate_UsesCurrentSensorValues()
    {
        var temperature =
            new PeriodicValueGenerator(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance);

        var environment =
            new EnvironmentSimulation(
                temperature,
                new ConstantValueGenerator(55.0),
                new ConstantValueGenerator(1013.0));

        var sensor =
            new SimulatedEnvironmentSensor(environment);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var adapter =
            new EnvironmentSensorRuntimeAdapter(
                sensor,
                runtimeInstrument);

        adapter.Publish(TestTimestamp);

        environment.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromHours(6),
                simulationTime: TimeSpan.FromHours(6)));

        var laterTimestamp =
            TestTimestamp.AddHours(6);

        adapter.Publish(laterTimestamp);

        AssertPropertyValue(
            runtimeInstrument,
            EnvironmentSensorDescriptorFactory.TemperaturePath,
            expectedValue: 25.0,
            expectedTimestamp: laterTimestamp,
            expectedQuality: PropertyQuality.Good);
    }

    [Fact]
    public void Publish_NotifiesRuntimeObservers()
    {
        var sensor =
            CreateConstantSensor(
                temperature: 21.5,
                relativeHumidity: 58.0,
                airPressure: 1012.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var observer =
            new RecordingObserver();

        runtimeInstrument.Subscribe(observer);

        var adapter =
            new EnvironmentSensorRuntimeAdapter(
                sensor,
                runtimeInstrument);

        adapter.Publish(TestTimestamp);

        Assert.Equal(
            3,
            observer.NotificationCount);
    }

    [Fact]
    public void Publish_NonUtcTimestamp_Throws()
    {
        var sensor =
            CreateConstantSensor(
                temperature: 21.5,
                relativeHumidity: 58.0,
                airPressure: 1012.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var adapter =
            new EnvironmentSensorRuntimeAdapter(
                sensor,
                runtimeInstrument);

        var nonUtcTimestamp =
            new DateTimeOffset(
                2026,
                7,
                10,
                12,
                0,
                0,
                TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => adapter.Publish(nonUtcTimestamp));
    }

    private static SimulatedEnvironmentSensor
        CreateConstantSensor(
            double temperature,
            double relativeHumidity,
            double airPressure)
    {
        var environment =
            new EnvironmentSimulation(
                new ConstantValueGenerator(temperature),
                new ConstantValueGenerator(relativeHumidity),
                new ConstantValueGenerator(airPressure));

        return new SimulatedEnvironmentSensor(
            environment);
    }

    private static RuntimeInstrument
        CreateRuntimeInstrument()
    {
        var descriptor =
            EnvironmentSensorDescriptorFactory
                .CreateDescriptor();

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId("simulation-endpoint"),
                [descriptor]);

        var context =
            new RuntimeContext();

        var endpoint =
            context.AddEndpoint(endpointDescriptor);

        return endpoint.FindInstrument(
            descriptor.Id)!;
    }

    private static void AssertPropertyValue(
        RuntimeInstrument runtimeInstrument,
        DescriptorPath path,
        double expectedValue,
        DateTimeOffset expectedTimestamp,
        PropertyQuality expectedQuality)
    {
        var property =
            runtimeInstrument.FindProperty(path);

        Assert.NotNull(property);
        Assert.NotNull(property.CurrentValue);

        var actualValue =
            Assert.IsType<double>(
                property.CurrentValue!.Value);

        Assert.Equal(
            expectedValue,
            actualValue,
            precision: 10);

        Assert.Equal(
            expectedTimestamp,
            property.CurrentValue.TimestampUtc);

        Assert.Equal(
            expectedQuality,
            property.CurrentValue.Quality);
    }

    private sealed class RecordingObserver
        : IPropertyValueObserver
    {
        public int NotificationCount { get; private set; }

        public void OnPropertyValueChanged(
            PropertyValueChanged change)
        {
            NotificationCount++;
        }
    }
}