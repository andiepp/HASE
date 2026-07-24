using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Adapts native Protocol Version 1 Command exchanges to the
/// transport-independent attachment operation port.
/// </summary>
internal sealed class NativeEndpointAttachmentCommandOperations
    : IEndpointAttachmentCommandOperations
{
    internal static TimeSpan DefaultOperationTimeout
    {
        get;
    } =
        TimeSpan.FromSeconds(
            5);

    private static int _nextCorrelationId;

    private readonly RuntimeEndpoint _runtimeEndpoint;
    private readonly TimeSpan _operationTimeout;

    private readonly Func<
        ProtocolMessage,
        TimeSpan,
        CancellationToken,
        Task<ProtocolMessage>>
        _exchangeAsync;

    internal NativeEndpointAttachmentCommandOperations(
        RuntimeEndpointConnectionCoordinator coordinator,
        TimeSpan operationTimeout)
        : this(
            (coordinator
                ?? throw new ArgumentNullException(
                    nameof(coordinator)))
                .RuntimeEndpoint,
            operationTimeout,
            (coordinator
                ?? throw new ArgumentNullException(
                    nameof(coordinator)))
                .ProbeAsync)
    {
    }

    internal NativeEndpointAttachmentCommandOperations(
        RuntimeEndpoint runtimeEndpoint,
        TimeSpan operationTimeout,
        Func<
            ProtocolMessage,
            TimeSpan,
            CancellationToken,
            Task<ProtocolMessage>>
            exchangeAsync)
    {
        _runtimeEndpoint =
            runtimeEndpoint
            ?? throw new ArgumentNullException(
                nameof(runtimeEndpoint));

        ValidateTimeout(
            operationTimeout);

        _operationTimeout =
            operationTimeout;

        _exchangeAsync =
            exchangeAsync
            ?? throw new ArgumentNullException(
                nameof(exchangeAsync));
    }

    /// <inheritdoc />
    public async Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
        InstrumentId instrumentId,
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ResolveRuntimeCommand(
            instrumentId,
            commandPath);

        cancellationToken.ThrowIfCancellationRequested();

        CorrelationId correlationId =
            CreateCorrelationId();

        var request =
            new ExecuteCommandRequest(
                correlationId,
                instrumentId,
                commandPath,
                argument);

        ProtocolMessage? responseMessage;

        try
        {
            responseMessage =
                await ExchangeAsync(
                    request,
                    cancellationToken);
        }
        catch (TimeoutException)
        {
            return CreateTimedOutResult();
        }

        if (responseMessage is null)
        {
            return CreateUnavailableResult();
        }

        if (responseMessage
            is not ExecuteCommandResponse response)
        {
            return EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.Failure,
                "The endpoint returned an unexpected Command response.");
        }

        return CompleteOperation(
            correlationId,
            response);
    }

    private async Task<ProtocolMessage?> ExchangeAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken)
    {
        if (_runtimeEndpoint.ConnectionStatus.State
            != EndpointConnectionState.Ready)
        {
            return null;
        }

        try
        {
            return await _exchangeAsync(
                request,
                _operationTimeout,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static EndpointAttachmentCommandOperationResult
        CompleteOperation(
            CorrelationId expectedCorrelationId,
            ExecuteCommandResponse response)
    {
        if (response.CorrelationId
            != expectedCorrelationId)
        {
            return EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.Failure,
                "The endpoint returned a mismatched correlation identifier.");
        }

        if (!response.Result.IsSuccess)
        {
            return EndpointAttachmentCommandOperationResult.Failed(
                MapFailureStatus(
                    response.Result.Code),
                response.Result.Message);
        }

        return EndpointAttachmentCommandOperationResult.Successful(
            response.ReturnValue);
    }

    private static EndpointAttachmentCommandOperationStatus MapFailureStatus(
        ProtocolResultCode resultCode)
    {
        return resultCode switch
        {
            ProtocolResultCode.InvalidRequest
                or ProtocolResultCode.NotSupported =>
                    EndpointAttachmentCommandOperationStatus
                        .ArgumentNotSupported,

            ProtocolResultCode.Rejected =>
                EndpointAttachmentCommandOperationStatus.Rejected,

            ProtocolResultCode.NotFound
                or ProtocolResultCode.InternalError
                or ProtocolResultCode.Success =>
                    EndpointAttachmentCommandOperationStatus.Failure,

            _ =>
                EndpointAttachmentCommandOperationStatus.Failure
        };
    }

    private void ResolveRuntimeCommand(
        InstrumentId instrumentId,
        DescriptorPath commandPath)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            commandPath);

        RuntimeInstrument runtimeInstrument =
            _runtimeEndpoint.FindInstrument(
                instrumentId)
            ?? throw new InvalidOperationException(
                $"Instrument '{instrumentId.Value}' is not present in the "
                + "attachment-bound runtime endpoint.");

        _ =
            runtimeInstrument.FindCommand(
                commandPath)
            ?? throw new InvalidOperationException(
                $"Command '{commandPath}' is not present in instrument "
                + $"'{instrumentId.Value}'.");
    }

    private static EndpointAttachmentCommandOperationResult
        CreateUnavailableResult()
    {
        return EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            "The attachment cannot currently perform the Command operation.");
    }

    private static EndpointAttachmentCommandOperationResult
        CreateTimedOutResult()
    {
        return EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.TimedOut,
            "The endpoint Command operation timed out.");
    }

    private static CorrelationId CreateCorrelationId()
    {
        uint value =
            unchecked(
                (uint)Interlocked.Increment(
                    ref _nextCorrelationId));

        if (value
            == CorrelationId.None.Value)
        {
            value =
                unchecked(
                    (uint)Interlocked.Increment(
                        ref _nextCorrelationId));
        }

        return new CorrelationId(
            value);
    }

    private static void ValidateTimeout(
        TimeSpan operationTimeout)
    {
        if (operationTimeout
            != Timeout.InfiniteTimeSpan
            && operationTimeout
                <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationTimeout),
                operationTimeout,
                "The Command operation timeout must be positive or "
                + "Timeout.InfiniteTimeSpan.");
        }
    }
}