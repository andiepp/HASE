using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Execution;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Protocol;

public sealed class RuntimeProtocolDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ShouldReturnDiscoverResponse()
    {
        // Arrange
        var context =
            new RuntimeContext();

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId("Endpoint1"));

        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                endpointDescriptor);

        var dispatcher =
            new RuntimeProtocolDispatcher(
                endpoint);

        var request =
            new DiscoverRequest(
                CorrelationId.None);

        // Act
        DiscoverResponse response =
            await dispatcher.DispatchAsync(
                request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            endpointDescriptor.Id,
            response.EndpointId);

        Assert.Empty(
            response.InstrumentIds);
    }

    [Fact]
    public async Task
        DispatchAsync_ReadEndpointDescriptor_ShouldReturnDescriptor()
    {
        // Arrange
        var context =
            new RuntimeContext();

        var descriptor =
            new EndpointDescriptor(
                new EndpointId("Endpoint1"));

        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                descriptor);

        var dispatcher =
            new RuntimeProtocolDispatcher(
                endpoint);

        var request =
            new ReadEndpointDescriptorRequest(
                CorrelationId.None,
                descriptor.Id);

        // Act
        ReadEndpointDescriptorResponse response =
            await dispatcher.DispatchAsync(
                request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            ProtocolResult.Success,
            response.Result);

        Assert.Same(
            descriptor,
            response.Descriptor);
    }

    [Fact]
    public async Task
        DispatchAsync_ReadProperty_ShouldReturnCurrentValue()
    {
        // Arrange
        TestRuntime runtime =
            CreateRuntime();

        var expectedValue =
            new PropertyValue(
                1_000_000.0,
                DateTimeOffset.UtcNow);

        bool updated =
            runtime.Instrument
                .UpdatePropertyValue(
                    runtime.PropertyDescriptor.Path,
                    expectedValue);

        Assert.True(updated);

        var dispatcher =
            new RuntimeProtocolDispatcher(
                runtime.Endpoint);

        var request =
            new ReadPropertyRequest(
                CorrelationId.None,
                runtime.InstrumentDescriptor.Id,
                runtime.PropertyDescriptor.Id);

        // Act
        ReadPropertyResponse response =
            await dispatcher.DispatchAsync(
                request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            ProtocolResult.Success,
            response.Result);

        Assert.Same(
            expectedValue,
            response.PropertyValue);
    }

    [Fact]
    public async Task
        DispatchAsync_WriteProperty_ShouldCallInstrumentExecutor()
    {
        // Arrange
        TestRuntime runtime =
            CreateRuntime();

        var executor =
            new RecordingInstrumentExecutor();

        runtime.Instrument
            .ConnectExecutor(
                executor);

        var dispatcher =
            new RuntimeProtocolDispatcher(
                runtime.Endpoint);

        var request =
            new WritePropertyRequest(
                CorrelationId.None,
                runtime.InstrumentDescriptor.Id,
                runtime.PropertyDescriptor.Id,
                Value: 23.0);

        // Act
        WritePropertyResponse response =
            await dispatcher.DispatchAsync(
                request);

        // Assert
        Assert.Equal(
            ProtocolResult.Success,
            response.Result);

        Assert.Equal(
            runtime.PropertyDescriptor.Id,
            executor.LastPropertyId);

        Assert.Equal(
            23.0,
            Assert.IsType<double>(
                executor.LastValue),
            precision: 10);

        Assert.Null(
            response.PropertyValue);
    }

    private static TestRuntime CreateRuntime()
    {
        var propertyDescriptor =
            new PropertyDescriptor(
                new PropertyId(
                    "DDS.Frequency"),
                DescriptorPath.Parse(
                    "DDS.Frequency"),
                "Frequency",
                new NumericDataDescriptor(
                    Quantities.Frequency,
                    Units.Hertz));

        var instrumentDescriptor =
            new InstrumentDescriptor(
                new InstrumentId("DDS"),
                "DDS Generator",
                new InstrumentKind(
                    "FrequencyGenerator"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            propertyDescriptor
                        ])
            };

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId("Endpoint1"),
                [instrumentDescriptor]);

        var context =
            new RuntimeContext();

        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                endpointDescriptor);

        RuntimeInstrument instrument =
            endpoint.FindInstrument(
                instrumentDescriptor.Id)!;

        return new TestRuntime(
            endpoint,
            instrument,
            instrumentDescriptor,
            propertyDescriptor);
    }

    private sealed record TestRuntime(
        RuntimeEndpoint Endpoint,
        RuntimeInstrument Instrument,
        InstrumentDescriptor InstrumentDescriptor,
        PropertyDescriptor PropertyDescriptor);

    private sealed class RecordingInstrumentExecutor
        : IInstrumentExecutor
    {
        public PropertyId? LastPropertyId
        {
            get;
            private set;
        }

        public object? LastValue
        {
            get;
            private set;
        }

        public Task<ExecutionResult<PropertyValue?>>
            ReadPropertyAsync(
                PropertyId propertyId,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                propertyId);

            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                new ExecutionResult<PropertyValue?>(
                    success: false,
                    value: null));
        }

        public Task<ExecutionResult>
            WritePropertyAsync(
                PropertyId propertyId,
                object? value,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                propertyId);

            cancellationToken
                .ThrowIfCancellationRequested();

            LastPropertyId =
                propertyId;

            LastValue =
                value;

            return Task.FromResult(
                ExecutionResult.Successful);
        }
    }
}