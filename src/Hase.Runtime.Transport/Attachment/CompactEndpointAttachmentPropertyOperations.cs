using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Adapts Compact Serial Protocol Property operations to the
/// transport-independent attachment operation port.
/// </summary>
internal sealed class CompactEndpointAttachmentPropertyOperations
    : IEndpointAttachmentPropertyOperations
{
    private readonly CompactPropertyMap _propertyMap;

    private readonly Func<
        byte,
        CancellationToken,
        Task<CompactRuntimePropertySynchronizationResult>>
        _readAsync;

    private readonly Func<
        byte,
        object,
        CancellationToken,
        Task<CompactRuntimePropertyWriteResult>>
        _writeAsync;

    internal CompactEndpointAttachmentPropertyOperations(
        CompactRuntimeEndpointConnectionCoordinator coordinator,
        CompactPropertyMap propertyMap)
        : this(
            propertyMap,
            (coordinator
                ?? throw new ArgumentNullException(
                    nameof(coordinator)))
                .ReadPropertyAsync,
            (coordinator
                ?? throw new ArgumentNullException(
                    nameof(coordinator)))
                .WritePropertyAsync)
    {
    }

    internal CompactEndpointAttachmentPropertyOperations(
        CompactPropertyMap propertyMap,
        Func<
            byte,
            CancellationToken,
            Task<CompactRuntimePropertySynchronizationResult>>
            readAsync,
        Func<
            byte,
            object,
            CancellationToken,
            Task<CompactRuntimePropertyWriteResult>>
            writeAsync)
    {
        _propertyMap =
            propertyMap
            ?? throw new ArgumentNullException(
                nameof(propertyMap));

        _readAsync =
            readAsync
            ?? throw new ArgumentNullException(
                nameof(readAsync));

        _writeAsync =
            writeAsync
            ?? throw new ArgumentNullException(
                nameof(writeAsync));
    }

    /// <inheritdoc />
    public async Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        CompactPropertyMapping? mapping =
            ResolveMapping(
                instrumentId,
                propertyId);

        cancellationToken.ThrowIfCancellationRequested();

        if (mapping is null)
        {
            return CreateNotSupportedResult();
        }

        try
        {
            CompactRuntimePropertySynchronizationResult result =
                await _readAsync(
                    mapping.CompactPropertyId,
                    cancellationToken);

            if (result.Status
                != CompactPropertyReadStatus.Success)
            {
                return EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.Failure);
            }

            return CreateSuccessfulResult(
                result.RuntimeProperty.CurrentValue);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return CreateTimedOutResult();
        }
        catch (InvalidDataException)
        {
            return CreateEndpointFailureResult();
        }
        catch (InvalidOperationException)
        {
            return CreateUnavailableResult();
        }
        catch (IOException)
        {
            return CreateUnavailableResult();
        }
    }

    /// <inheritdoc />
    public async Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        CompactPropertyMapping? mapping =
            ResolveMapping(
                instrumentId,
                propertyId);

        cancellationToken.ThrowIfCancellationRequested();

        if (mapping is null)
        {
            return CreateNotSupportedResult();
        }

        if (requestedValue is null)
        {
            return EndpointAttachmentPropertyOperationResult.Failed(
                EndpointAttachmentPropertyOperationStatus.InvalidValue);
        }

        try
        {
            CompactRuntimePropertyWriteResult result =
                await _writeAsync(
                    mapping.CompactPropertyId,
                    requestedValue,
                    cancellationToken);

            EndpointAttachmentPropertyOperationStatus? failureStatus =
                MapWriteFailureStatus(
                    result);

            if (failureStatus.HasValue)
            {
                return EndpointAttachmentPropertyOperationResult.Failed(
                    failureStatus.Value);
            }

            return CreateSuccessfulResult(
                result.RuntimeProperty.CurrentValue);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return CreateTimedOutResult();
        }
        catch (InvalidDataException)
        {
            return CreateEndpointFailureResult();
        }
        catch (InvalidOperationException)
        {
            return CreateUnavailableResult();
        }
        catch (IOException)
        {
            return CreateUnavailableResult();
        }
    }

    private CompactPropertyMapping? ResolveMapping(
        InstrumentId instrumentId,
        PropertyId propertyId)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            propertyId);

        return _propertyMap.Find(
            instrumentId,
            propertyId);
    }

    private static EndpointAttachmentPropertyOperationStatus?
        MapWriteFailureStatus(
            CompactRuntimePropertyWriteResult result)
    {
        return result.WriteStatus switch
        {
            CompactPropertyWriteStatus.UnknownProperty =>
                EndpointAttachmentPropertyOperationStatus.Failure,

            CompactPropertyWriteStatus.WriteNotSupported =>
                EndpointAttachmentPropertyOperationStatus.NotSupported,

            CompactPropertyWriteStatus.InvalidValue =>
                EndpointAttachmentPropertyOperationStatus.InvalidValue,

            CompactPropertyWriteStatus.WriteFailed =>
                EndpointAttachmentPropertyOperationStatus.Failure,

            CompactPropertyWriteStatus.Success
                when result.ConfirmationReadStatus
                    == CompactPropertyReadStatus.Success =>
                        null,

            CompactPropertyWriteStatus.Success =>
                EndpointAttachmentPropertyOperationStatus.Failure,

            _ =>
                EndpointAttachmentPropertyOperationStatus.Failure
        };
    }

    private static EndpointAttachmentPropertyOperationResult
        CreateSuccessfulResult(
            PropertyValue? confirmedValue)
    {
        if (confirmedValue is null)
        {
            return CreateEndpointFailureResult();
        }

        return EndpointAttachmentPropertyOperationResult.Successful(
            confirmedValue);
    }

    private static EndpointAttachmentPropertyOperationResult
        CreateNotSupportedResult()
    {
        return EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.NotSupported);
    }

    private static EndpointAttachmentPropertyOperationResult
        CreateEndpointFailureResult()
    {
        return EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.Failure);
    }

    private static EndpointAttachmentPropertyOperationResult
        CreateUnavailableResult()
    {
        return EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            "The compact attachment cannot currently perform the "
            + "Property operation.");
    }

    private static EndpointAttachmentPropertyOperationResult
        CreateTimedOutResult()
    {
        return EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.TimedOut,
            "The compact endpoint Property operation timed out.");
    }
}