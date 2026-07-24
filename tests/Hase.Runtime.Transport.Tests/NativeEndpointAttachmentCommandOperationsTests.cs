using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeEndpointAttachmentCommandOperationsTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly DescriptorPath CommandPath =
        new(
            "Controller",
            "ToggleLed");

    [Fact]
    public async Task ExecuteAsync_PassesArgumentAndReturnValue()
    {
        RuntimeEndpoint endpoint =
            CreateReadyEndpoint();

        ExecuteCommandRequest? capturedRequest =
            null;

        int exchangeCount =
            0;

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                {
                    exchangeCount++;

                    capturedRequest =
                        Assert.IsType<ExecuteCommandRequest>(
                            request);

                    return Task.FromResult<ProtocolMessage>(
                        new ExecuteCommandResponse(
                            capturedRequest.CorrelationId,
                            ProtocolResult.Success,
                            ReturnValue: "confirmed"));
                });

        object argument =
            42;

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            "confirmed",
            result.ReturnValue);

        Assert.Equal(
            1,
            exchangeCount);

        Assert.NotNull(
            capturedRequest);

        Assert.Equal(
            InstrumentId,
            capturedRequest.InstrumentId);

        Assert.Equal(
            CommandPath,
            capturedRequest.CommandPath);

        Assert.Same(
            argument,
            capturedRequest.Argument);
    }

    [Fact]
    public async Task ExecuteAsync_NullArgumentAndReturnValue_Succeeds()
    {
        RuntimeEndpoint endpoint =
            CreateReadyEndpoint();

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                {
                    var commandRequest =
                        Assert.IsType<ExecuteCommandRequest>(
                            request);

                    Assert.Null(
                        commandRequest.Argument);

                    return Task.FromResult<ProtocolMessage>(
                        new ExecuteCommandResponse(
                            commandRequest.CorrelationId,
                            ProtocolResult.Success,
                            ReturnValue: null));
                });

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.True(
            result.IsSuccess);

        Assert.Null(
            result.ReturnValue);
    }

    [Fact]
    public async Task ExecuteAsync_NotReady_DoesNotSubmit()
    {
        RuntimeEndpoint endpoint =
            CreateEndpoint();

        int exchangeCount =
            0;

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                {
                    exchangeCount++;

                    throw new InvalidOperationException();
                });

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            result.Status);

        Assert.Equal(
            0,
            exchangeCount);
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_SubmitsExactlyOnce()
    {
        RuntimeEndpoint endpoint =
            CreateReadyEndpoint();

        int exchangeCount =
            0;

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                {
                    exchangeCount++;

                    return Task.FromException<ProtocolMessage>(
                        new TimeoutException());
                });

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.TimedOut,
            result.Status);

        Assert.Equal(
            1,
            exchangeCount);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelled_DoesNotSubmit()
    {
        RuntimeEndpoint endpoint =
            CreateReadyEndpoint();

        int exchangeCount =
            0;

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                {
                    exchangeCount++;

                    throw new InvalidOperationException();
                });

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null,
                cancellationSource.Token));

        Assert.Equal(
            0,
            exchangeCount);
    }

    [Theory]
    [InlineData(
        ProtocolResultCode.InvalidRequest,
        EndpointAttachmentCommandOperationStatus.ArgumentNotSupported)]
    [InlineData(
        ProtocolResultCode.NotSupported,
        EndpointAttachmentCommandOperationStatus.ArgumentNotSupported)]
    [InlineData(
        ProtocolResultCode.Rejected,
        EndpointAttachmentCommandOperationStatus.Rejected)]
    [InlineData(
        ProtocolResultCode.NotFound,
        EndpointAttachmentCommandOperationStatus.Failure)]
    [InlineData(
        ProtocolResultCode.InternalError,
        EndpointAttachmentCommandOperationStatus.Failure)]
    public async Task ExecuteAsync_MapsProtocolFailure(
        ProtocolResultCode protocolStatus,
        EndpointAttachmentCommandOperationStatus expectedStatus)
    {
        RuntimeEndpoint endpoint =
            CreateReadyEndpoint();

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                {
                    var commandRequest =
                        Assert.IsType<ExecuteCommandRequest>(
                            request);

                    return Task.FromResult<ProtocolMessage>(
                        new ExecuteCommandResponse(
                            commandRequest.CorrelationId,
                            new ProtocolResult(
                                protocolStatus,
                                " endpoint diagnostic "),
                            ReturnValue: null));
                });

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.Equal(
            expectedStatus,
            result.Status);

        Assert.Equal(
            "endpoint diagnostic",
            result.Diagnostic);

        Assert.Null(
            result.ReturnValue);
    }

    [Fact]
    public async Task ExecuteAsync_MismatchedCorrelation_Fails()
    {
        RuntimeEndpoint endpoint =
            CreateReadyEndpoint();

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                    Task.FromResult<ProtocolMessage>(
                        new ExecuteCommandResponse(
                            new CorrelationId(
                                uint.MaxValue),
                            ProtocolResult.Success,
                            ReturnValue: null)));

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Failure,
            result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedResponse_Fails()
    {
        RuntimeEndpoint endpoint =
            CreateReadyEndpoint();

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                {
                    var commandRequest =
                        Assert.IsType<ExecuteCommandRequest>(
                            request);

                    return Task.FromResult<ProtocolMessage>(
                        new ReadPropertyRequest(
                            commandRequest.CorrelationId,
                            InstrumentId,
                            new PropertyId(
                                "state")));
                });

        EndpointAttachmentCommandOperationResult result =
            await operations.ExecuteAsync(
                InstrumentId,
                CommandPath,
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Failure,
            result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommand_DoesNotSubmit()
    {
        RuntimeEndpoint endpoint =
            CreateReadyEndpoint();

        int exchangeCount =
            0;

        var operations =
            new NativeEndpointAttachmentCommandOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (request, timeout, cancellationToken) =>
                {
                    exchangeCount++;

                    throw new InvalidOperationException();
                });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => operations.ExecuteAsync(
                InstrumentId,
                new DescriptorPath(
                    "Controller",
                    "Unknown"),
                argument: null));

        Assert.Equal(
            0,
            exchangeCount);
    }

    private static RuntimeEndpoint CreateReadyEndpoint()
    {
        RuntimeEndpoint endpoint =
            CreateEndpoint();

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));

        return endpoint;
    }

    private static RuntimeEndpoint CreateEndpoint()
    {
        var commandDescriptor =
            new CommandDescriptor(
                CommandPath,
                "Toggle LED");

        var instrumentDescriptor =
            new InstrumentDescriptor(
                InstrumentId,
                "Controller",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        commands:
                        [
                            commandDescriptor
                        ])
            };

        return new RuntimeEndpoint(
            new RuntimeContext(),
            new EndpointDescriptor(
                new EndpointId(
                    "native-command-endpoint"),
                [
                    instrumentDescriptor
                ]));
    }
}