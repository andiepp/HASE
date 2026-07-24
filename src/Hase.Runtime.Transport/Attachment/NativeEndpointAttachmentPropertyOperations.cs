using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Adapts native Protocol Version 1 Property exchanges to the
/// transport-independent attachment operation port.
/// </summary>
internal sealed class NativeEndpointAttachmentPropertyOperations
    : IEndpointAttachmentPropertyOperations
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

    internal NativeEndpointAttachmentPropertyOperations(
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

    internal NativeEndpointAttachmentPropertyOperations(
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
    public async Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        RuntimeProperty runtimeProperty =
            ResolveRuntimeProperty(
                instrumentId,
                propertyId);

        cancellationToken.ThrowIfCancellationRequested();

        CorrelationId correlationId =
            CreateCorrelationId();

        var request =
            new ReadPropertyRequest(
                correlationId,
                instrumentId,
                propertyId);

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
            is not ReadPropertyResponse response)
        {
            return EndpointAttachmentPropertyOperationResult.Failed(
                EndpointAttachmentPropertyOperationStatus.Failure,
                "The endpoint returned an unexpected Property-read response.");
        }

        return CompleteOperation(
            runtimeProperty,
            correlationId,
            response.CorrelationId,
            response.Result,
            response.PropertyValue,
            invalidRequestStatus:
                EndpointAttachmentPropertyOperationStatus.Failure);
    }

    /// <inheritdoc />
    public async Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        RuntimeProperty runtimeProperty =
            ResolveRuntimeProperty(
                instrumentId,
                propertyId);

        cancellationToken.ThrowIfCancellationRequested();

        CorrelationId correlationId =
            CreateCorrelationId();

        var request =
            new WritePropertyRequest(
                correlationId,
                instrumentId,
                propertyId,
                requestedValue);

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
            is not WritePropertyResponse response)
        {
            return EndpointAttachmentPropertyOperationResult.Failed(
                EndpointAttachmentPropertyOperationStatus.Failure,
                "The endpoint returned an unexpected Property-write response.");
        }

        return CompleteOperation(
            runtimeProperty,
            correlationId,
            response.CorrelationId,
            response.Result,
            response.PropertyValue,
            invalidRequestStatus:
                EndpointAttachmentPropertyOperationStatus.InvalidValue);
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

    private static EndpointAttachmentPropertyOperationResult
        CompleteOperation(
            RuntimeProperty runtimeProperty,
            CorrelationId expectedCorrelationId,
            CorrelationId actualCorrelationId,
            ProtocolResult protocolResult,
            PropertyValue? confirmedValue,
            EndpointAttachmentPropertyOperationStatus invalidRequestStatus)
    {
        if (actualCorrelationId
            != expectedCorrelationId)
        {
            return EndpointAttachmentPropertyOperationResult.Failed(
                EndpointAttachmentPropertyOperationStatus.Failure,
                "The endpoint returned a mismatched correlation identifier.");
        }

        if (!protocolResult.IsSuccess)
        {
            return EndpointAttachmentPropertyOperationResult.Failed(
                MapFailureStatus(
                    protocolResult.Code,
                    invalidRequestStatus));
        }

        if (confirmedValue is null)
        {
            return EndpointAttachmentPropertyOperationResult.Failed(
                EndpointAttachmentPropertyOperationStatus.Failure,
                "The successful endpoint response contained no confirmed value.");
        }

        runtimeProperty.UpdateValue(
            confirmedValue);

        return EndpointAttachmentPropertyOperationResult.Successful(
            confirmedValue);
    }

    private static EndpointAttachmentPropertyOperationStatus MapFailureStatus(
        ProtocolResultCode resultCode,
        EndpointAttachmentPropertyOperationStatus invalidRequestStatus)
    {
        return resultCode switch
        {
            ProtocolResultCode.InvalidRequest =>
                invalidRequestStatus,

            ProtocolResultCode.NotSupported =>
                EndpointAttachmentPropertyOperationStatus.NotSupported,

            ProtocolResultCode.Rejected =>
                EndpointAttachmentPropertyOperationStatus.Rejected,

            ProtocolResultCode.NotFound
                or ProtocolResultCode.InternalError
                or ProtocolResultCode.Success =>
                    EndpointAttachmentPropertyOperationStatus.Failure,

            _ =>
                EndpointAttachmentPropertyOperationStatus.Failure
        };
    }

    private RuntimeProperty ResolveRuntimeProperty(
        InstrumentId instrumentId,
        PropertyId propertyId)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            propertyId);

        RuntimeInstrument runtimeInstrument =
            _runtimeEndpoint.FindInstrument(
                instrumentId)
            ?? throw new InvalidOperationException(
                $"Instrument '{instrumentId.Value}' is not present in the "
                + "attachment-bound runtime endpoint.");

        return runtimeInstrument.FindProperty(
                propertyId)
            ?? throw new InvalidOperationException(
                $"Property '{propertyId.Value}' is not present in instrument "
                + $"'{instrumentId.Value}'.");
    }

    private static EndpointAttachmentPropertyOperationResult
        CreateUnavailableResult()
    {
        return EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            "The attachment cannot currently perform the Property operation.");
    }

    private static EndpointAttachmentPropertyOperationResult
        CreateTimedOutResult()
    {
        return EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.TimedOut,
            "The endpoint Property operation timed out.");
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
                "The Property operation timeout must be positive or "
                + "Timeout.InfiniteTimeSpan.");
        }
    }
}