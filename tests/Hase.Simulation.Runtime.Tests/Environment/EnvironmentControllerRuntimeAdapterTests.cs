using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Simulation.Environment;
using Hase.Simulation.Runtime.Environment;

namespace Hase.Simulation.Runtime.Tests.Environment;

public sealed class EnvironmentControllerRuntimeAdapterTests
{
    private static readonly DateTimeOffset TestTimestamp =
        new(
            year: 2026,
            month: 7,
            day: 12,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

    [Fact]
    public void Constructor_NullSimulation_Throws()
    {
        var runtimeInstrument =
            CreateRuntimeInstrument();

        Assert.Throws<ArgumentNullException>(
            () => new EnvironmentControllerRuntimeAdapter(
                null!,
                runtimeInstrument));
    }

    [Fact]
    public void Constructor_NullRuntimeInstrument_Throws()
    {
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        Assert.Throws<ArgumentNullException>(
            () => new EnvironmentControllerRuntimeAdapter(
                simulation,
                null!));
    }

    [Fact]
    public void Constructor_IncompatibleInstrument_Throws()
    {
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var context =
            new RuntimeContext();

        var endpoint =
            context.AddEndpoint(
                new EndpointDescriptor(
                    new EndpointId("simulation-endpoint")));

        var incompatibleInstrument =
            new RuntimeInstrument(
                endpoint,
                new InstrumentDescriptor(
                    new InstrumentId("incompatible"),
                    "Incompatible",
                    new InstrumentKind("test")));

        Assert.Throws<ArgumentException>(
            () => new EnvironmentControllerRuntimeAdapter(
                simulation,
                incompatibleInstrument));
    }

    [Fact]
    public void Publish_WritesTargetTemperature()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var adapter =
            new EnvironmentControllerRuntimeAdapter(
                simulation,
                runtimeInstrument);

        // Act
        adapter.Publish(TestTimestamp);

        // Assert
        var property =
            runtimeInstrument.FindProperty(
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId);

        Assert.NotNull(property);
        Assert.NotNull(property.CurrentValue);

        var actualValue =
            Assert.IsType<double>(
                property.CurrentValue!.Value);

        Assert.Equal(
            21.5,
            actualValue,
            precision: 10);

        Assert.Equal(
            TestTimestamp,
            property.CurrentValue.TimestampUtc);

        Assert.Equal(
            PropertyQuality.Good,
            property.CurrentValue.Quality);
    }

    [Fact]
    public void Publish_AfterSimulationWrite_UsesCurrentState()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var adapter =
            new EnvironmentControllerRuntimeAdapter(
                simulation,
                runtimeInstrument);

        simulation.SetTargetTemperature(
            23.0);

        // Act
        adapter.Publish(TestTimestamp);

        // Assert
        var property =
            runtimeInstrument.FindProperty(
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId);

        Assert.NotNull(property);
        Assert.NotNull(property.CurrentValue);

        var actualValue =
            Assert.IsType<double>(
                property.CurrentValue!.Value);

        Assert.Equal(
            23.0,
            actualValue,
            precision: 10);
    }

    [Fact]
    public void Publish_UsesSpecifiedQuality()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var adapter =
            new EnvironmentControllerRuntimeAdapter(
                simulation,
                runtimeInstrument);

        // Act
        adapter.Publish(
            TestTimestamp,
            PropertyQuality.Uncertain);

        // Assert
        var property =
            runtimeInstrument.FindProperty(
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId);

        Assert.NotNull(property);
        Assert.NotNull(property.CurrentValue);

        Assert.Equal(
            PropertyQuality.Uncertain,
            property.CurrentValue!.Quality);
    }

    [Fact]
    public void Publish_NotifiesRuntimeObservers()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var observer =
            new RecordingObserver();

        runtimeInstrument.Subscribe(observer);

        var adapter =
            new EnvironmentControllerRuntimeAdapter(
                simulation,
                runtimeInstrument);

        // Act
        adapter.Publish(TestTimestamp);

        // Assert
        Assert.Equal(
            1,
            observer.NotificationCount);
    }

    [Fact]
    public void Publish_NonUtcTimestamp_Throws()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var runtimeInstrument =
            CreateRuntimeInstrument();

        var adapter =
            new EnvironmentControllerRuntimeAdapter(
                simulation,
                runtimeInstrument);

        var nonUtcTimestamp =
            new DateTimeOffset(
                2026,
                7,
                12,
                12,
                0,
                0,
                TimeSpan.FromHours(2));

        // Act and Assert
        Assert.Throws<ArgumentException>(
            () => adapter.Publish(nonUtcTimestamp));
    }

    private static EnvironmentControllerSimulation
        CreateSimulation(
            double targetTemperature)
    {
        var state =
            new EnvironmentControllerState(
                targetTemperature);

        return new EnvironmentControllerSimulation(
            state);
    }

    private static RuntimeInstrument
        CreateRuntimeInstrument()
    {
        var descriptor =
            EnvironmentControllerDescriptorFactory
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