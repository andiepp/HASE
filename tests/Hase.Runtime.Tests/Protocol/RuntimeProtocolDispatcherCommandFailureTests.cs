using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Execution;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Protocol;

public sealed class RuntimeProtocolDispatcherCommandFailureTests
{
    [Fact]
    public async Task
        DispatchAsync_ExecuteCommand_UnknownInstrument_ShouldReturnNotFound()
    {
        // Arrange
        TestRuntime runtime =
            CreateRuntime();

        var dispatcher =
            new RuntimeProtocolDispatcher(
                runtime.Endpoint);

        var request =
            new ExecuteCommandRequest(
                CorrelationId.None,
                new InstrumentId(
                    "Unknown"),
                runtime.CommandDescriptor.Path,
                Argument: null);

        // Act
        ExecuteCommandResponse response =
            await dispatcher.DispatchAsync(
                request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            ProtocolResult.NotFound,
            response.Result);

        Assert.Null(
            response.ReturnValue);
    }

    [Fact]
    public async Task
        DispatchAsync_ExecuteCommand_UnknownCommand_ShouldReturnNotFound()
    {
        // Arrange
        TestRuntime runtime =
            CreateRuntime();

        var dispatcher =
            new RuntimeProtocolDispatcher(
                runtime.Endpoint);

        var request =
            new ExecuteCommandRequest(
                CorrelationId.None,
                runtime.InstrumentDescriptor.Id,
                DescriptorPath.Parse(
                    "DDS.Unknown"),
                Argument: null);

        // Act
        ExecuteCommandResponse response =
            await dispatcher.DispatchAsync(
                request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            ProtocolResult.NotFound,
            response.Result);

        Assert.Null(
            response.ReturnValue);
    }

    [Fact]
    public async Task
        DispatchAsync_ExecuteCommand_RejectedExecution_ShouldReturnRejected()
    {
        // Arrange
        TestRuntime runtime =
            CreateRuntime();

        var executor =
            new RejectingInstrumentExecutor();

        runtime.Instrument
            .ConnectExecutor(
                executor);

        var dispatcher =
            new RuntimeProtocolDispatcher(
                runtime.Endpoint);

        var request =
            new ExecuteCommandRequest(
                CorrelationId.None,
                runtime.InstrumentDescriptor.Id,
                runtime.CommandDescriptor.Path,
                Argument: "Reset");

        // Act
        ExecuteCommandResponse response =
            await dispatcher.DispatchAsync(
                request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            ProtocolResult.Rejected,
            response.Result);

        Assert.Null(
            response.ReturnValue);

        Assert.Equal(
            runtime.CommandDescriptor.Path,
            executor.LastCommandPath);

        Assert.Equal(
            "Reset",
            Assert.IsType<string>(
                executor.LastArgument));
    }

    private static TestRuntime CreateRuntime()
    {
        var commandDescriptor =
            new CommandDescriptor(
                DescriptorPath.Parse(
                    "DDS.Reset"),
                "Reset");

        var instrumentDescriptor =
            new InstrumentDescriptor(
                new InstrumentId(
                    "DDS"),
                "DDS Generator",
                new InstrumentKind(
                    "FrequencyGenerator"))
            {
                Interface =
                    new InstrumentInterface(
                        commands:
                        [
                            commandDescriptor
                        ])
            };

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "Endpoint1"),
                [
                    instrumentDescriptor
                ]);

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
            commandDescriptor);
    }

    private sealed record TestRuntime(
        RuntimeEndpoint Endpoint,
        RuntimeInstrument Instrument,
        InstrumentDescriptor InstrumentDescriptor,
        CommandDescriptor CommandDescriptor);

    private sealed class RejectingInstrumentExecutor
        : IInstrumentExecutor
    {
        public DescriptorPath? LastCommandPath
        {
            get;
            private set;
        }

        public object? LastArgument
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

            return Task.FromResult(
                ExecutionResult.Failed);
        }

        public Task<ExecutionResult<object?>>
            ExecuteCommandAsync(
                DescriptorPath commandPath,
                object? argument,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                commandPath);

            cancellationToken
                .ThrowIfCancellationRequested();

            LastCommandPath = commandPath;
            LastArgument = argument;

            return Task.FromResult(
                new ExecutionResult<object?>(
                    success: false,
                    value: null));
        }
    }
}