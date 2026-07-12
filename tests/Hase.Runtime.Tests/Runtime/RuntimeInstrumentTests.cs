using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Execution;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Runtime;

public sealed class RuntimeInstrumentTests
{
    [Fact]
    public void Constructor_ShouldUseNullInstrumentExecutor()
    {
        // Arrange and Act
        var instrument =
            CreateRuntimeInstrument();

        // Assert
        Assert.IsType<NullInstrumentExecutor>(
            instrument.Executor);
    }

    [Fact]
    public void ConnectExecutor_ShouldConnectExecutor()
    {
        // Arrange
        var instrument =
            CreateRuntimeInstrument();

        var executor =
            new TestInstrumentExecutor();

        // Act
        instrument.ConnectExecutor(executor);

        // Assert
        Assert.Same(
            executor,
            instrument.Executor);
    }

    [Fact]
    public void ConnectExecutor_NullExecutor_ShouldThrow()
    {
        // Arrange
        var instrument =
            CreateRuntimeInstrument();

        // Act
        var exception =
            Assert.Throws<ArgumentNullException>(
                () => instrument.ConnectExecutor(null!));

        // Assert
        Assert.Equal(
            "executor",
            exception.ParamName);
    }

    [Fact]
    public void ConnectExecutor_NullInstrumentExecutor_ShouldThrow()
    {
        // Arrange
        var instrument =
            CreateRuntimeInstrument();

        var executor =
            new NullInstrumentExecutor();

        // Act
        var exception =
            Assert.Throws<ArgumentException>(
                () => instrument.ConnectExecutor(executor));

        // Assert
        Assert.Equal(
            "executor",
            exception.ParamName);

        Assert.IsType<NullInstrumentExecutor>(
            instrument.Executor);
    }

    [Fact]
    public void ConnectExecutor_SecondExecutor_ShouldThrow()
    {
        // Arrange
        var instrument =
            CreateRuntimeInstrument();

        var firstExecutor =
            new TestInstrumentExecutor();

        var secondExecutor =
            new TestInstrumentExecutor();

        instrument.ConnectExecutor(firstExecutor);

        // Act
        Assert.Throws<InvalidOperationException>(
            () => instrument.ConnectExecutor(
                secondExecutor));

        // Assert
        Assert.Same(
            firstExecutor,
            instrument.Executor);
    }

    private static RuntimeInstrument
        CreateRuntimeInstrument()
    {
        var instrumentDescriptor =
            new InstrumentDescriptor(
                new InstrumentId("test-instrument"),
                "Test instrument",
                new InstrumentKind("test"));

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId("test-endpoint"),
                [instrumentDescriptor]);

        var context =
            new RuntimeContext();

        var endpoint =
            context.AddEndpoint(endpointDescriptor);

        return endpoint.FindInstrument(
            instrumentDescriptor.Id)!;
    }

    private sealed class TestInstrumentExecutor
        : IInstrumentExecutor
    {
        public Task<ExecutionResult<PropertyValue?>>
            ReadPropertyAsync(
                PropertyId propertyId,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(propertyId);
            cancellationToken.ThrowIfCancellationRequested();

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
            ArgumentNullException.ThrowIfNull(propertyId);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                ExecutionResult.Failed);
        }
    }
}