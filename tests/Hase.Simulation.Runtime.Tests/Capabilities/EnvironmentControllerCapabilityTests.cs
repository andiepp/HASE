using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;
using Hase.Simulation.Environment;
using Hase.Simulation.Runtime.Environment;

namespace Hase.Simulation.Runtime.Tests.Capabilities;

public sealed class EnvironmentControllerCapabilityTests
{
    private static readonly DateTimeOffset InitialTimestamp =
        new(
            year: 2026,
            month: 7,
            day: 12,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

    [Fact]
    public async Task
        C001_ClientCanChangeSimulatedTargetTemperature()
    {
        // Arrange
        var simulation =
            new EnvironmentControllerSimulation(
                new EnvironmentControllerState(
                    targetTemperature: 21.5));

        var descriptor =
            EnvironmentControllerDescriptorFactory
                .CreateDescriptor();

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "simulation-endpoint"),
                [descriptor]);

        var context =
            new RuntimeContext();

        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                endpointDescriptor);

        RuntimeInstrument runtimeInstrument =
            endpoint.FindInstrument(
                descriptor.Id)!;

        var executor =
            new EnvironmentControllerInstrumentExecutor(
                simulation);

        runtimeInstrument.ConnectExecutor(
            executor);

        var adapter =
            new EnvironmentControllerRuntimeAdapter(
                simulation,
                runtimeInstrument);

        var dispatcher =
            new RuntimeProtocolDispatcher(
                endpoint);

        adapter.Publish(
            InitialTimestamp);

        var writeRequest =
            new WritePropertyRequest(
                CorrelationId.None,
                EnvironmentControllerDescriptorFactory
                    .InstrumentId,
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId,
                Value: 23.0);

        // Act
        WritePropertyResponse writeResponse =
            await dispatcher.DispatchAsync(
                writeRequest);

        adapter.Publish(
            InitialTimestamp.AddSeconds(1));

        var readRequest =
            new ReadPropertyRequest(
                CorrelationId.None,
                EnvironmentControllerDescriptorFactory
                    .InstrumentId,
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId);

        ReadPropertyResponse readResponse =
            await dispatcher.DispatchAsync(
                readRequest);

        // Assert
        Assert.Equal(
            ProtocolResult.Success,
            writeResponse.Result);

        Assert.Equal(
            23.0,
            simulation.State.TargetTemperature,
            precision: 10);

        Assert.Equal(
            ProtocolResult.Success,
            readResponse.Result);

        Assert.NotNull(
            readResponse.PropertyValue);

        var confirmedValue =
            Assert.IsType<double>(
                readResponse.PropertyValue!.Value);

        Assert.Equal(
            23.0,
            confirmedValue,
            precision: 10);

        Assert.Equal(
            InitialTimestamp.AddSeconds(1),
            readResponse.PropertyValue.TimestampUtc);

        Assert.Equal(
            PropertyQuality.Good,
            readResponse.PropertyValue.Quality);
    }
}