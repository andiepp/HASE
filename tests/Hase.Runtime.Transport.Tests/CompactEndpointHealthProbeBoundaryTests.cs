using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointHealthProbeBoundaryTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    [Fact]
    public void Constructor_NullCoordinator_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        void Act()
        {
            _ = new CompactEndpointHealthProbe(
                null!,
                CreatePropertyMap(
                    definition),
                CompactEndpointHealthProbeOptions.Default);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullPropertyMap_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition);

        void Act()
        {
            _ = new CompactEndpointHealthProbe(
                coordinator,
                null!,
                CompactEndpointHealthProbeOptions.Default);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullOptions_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition);

        void Act()
        {
            _ = new CompactEndpointHealthProbe(
                coordinator,
                CreatePropertyMap(
                    definition),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task ProbeAsync_NoActiveConnection_ShouldThrowWithoutChangingState()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var connectionFactory =
            new TestCompactEndpointConnectionFactory();

        var coordinator =
            CreateCoordinator(
                definition,
                runtimeEndpoint,
                connectionFactory);

        var probe =
            new CompactEndpointHealthProbe(
                coordinator,
                CreatePropertyMap(
                    definition),
                CompactEndpointHealthProbeOptions.Default);

        async Task Act()
        {
            await probe.ProbeAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            0,
            connectionFactory.ConnectCallCount);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task ProbeAsync_PreCancelled_ShouldRemainDisconnected()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var connectionFactory =
            new TestCompactEndpointConnectionFactory();

        var coordinator =
            CreateCoordinator(
                definition,
                runtimeEndpoint,
                connectionFactory);

        var probe =
            new CompactEndpointHealthProbe(
                coordinator,
                CreatePropertyMap(
                    definition),
                CompactEndpointHealthProbeOptions.Default);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            await probe.ProbeAsync(
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            0,
            connectionFactory.ConnectCallCount);

        await coordinator.DisposeAsync();
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            EndpointDescriptorDefinition definition)
    {
        return CreateCoordinator(
            definition,
            CreateRuntimeEndpoint(
                definition),
            new TestCompactEndpointConnectionFactory());
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            EndpointDescriptorDefinition definition,
            RuntimeEndpoint runtimeEndpoint,
            ICompactEndpointConnectionFactory connectionFactory)
    {
        return new CompactRuntimeEndpointConnectionCoordinator(
            connectionFactory,
            new SerialTransportOptions(
                "COM10",
                115200),
            CreatePropertyMap(
                definition),
            runtimeEndpoint,
            new EndpointDescriptorCompatibilityValidator());
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        EndpointDescriptorDefinition definition)
    {
        return new RuntimeContext()
            .AddEndpoint(
                definition.Materialize(
                    new EndpointId(
                        "arduino-uno-01")));
    }

    private static EndpointDescriptorDefinition CreateDefinition()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.Read
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            property
                        ])
            };

        return new EndpointDescriptorDefinition(
            metadata:
                new(),
            instruments:
            [
                instrument
            ]);
    }

    private static CompactPropertyMap CreatePropertyMap(
        EndpointDescriptorDefinition definition)
    {
        return new CompactPropertyMap(
            definition,
            mappings:
            [
                new CompactPropertyMapping(
                    compactPropertyId: 0x01,
                    InstrumentId,
                    PropertyId,
                    CompactPropertyValueEncoding.Boolean)
            ]);
    }

    private sealed class TestCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            throw new NotSupportedException(
                "This boundary test does not establish a connection.");
        }
    }
}