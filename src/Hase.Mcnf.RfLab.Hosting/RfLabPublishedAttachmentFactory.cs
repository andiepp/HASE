using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Mcnf.RfLab.Hosting;

/// <summary>
/// Opens, completely synchronizes, and then publishes one RF-Lab attachment.
/// </summary>
public sealed class RfLabPublishedAttachmentFactory
{
    private readonly RuntimeContext runtimeContext;
    private readonly RfLabOperationalConnectionFactory operationalConnectionFactory;
    private readonly TimeProvider timeProvider;

    public RfLabPublishedAttachmentFactory(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory,
        TimeSpan? settleDelay = null,
        TimeProvider? timeProvider = null)
    {
        this.runtimeContext = runtimeContext
            ?? throw new ArgumentNullException(nameof(runtimeContext));
        operationalConnectionFactory = new RfLabOperationalConnectionFactory(
            runtimeContext,
            serialByteStreamFactory
                ?? throw new ArgumentNullException(nameof(serialByteStreamFactory)),
            settleDelay,
            timeProvider);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RfLabPublishedAttachment> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
        => await OpenAsync(
            endpointId,
            RfLabReadOnlyDefinition.EndpointDefinition,
            serialOptions,
            cancellationToken).ConfigureAwait(false);

    public async Task<RfLabPublishedAttachment> OpenAsync(
        EndpointId endpointId,
        EndpointDescriptorDefinition definition,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        RfLabOperationalConnection? connection = null;
        try
        {
            connection = await operationalConnectionFactory
                .OpenAsync(endpointId, definition, serialOptions, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            connection.RuntimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(EndpointConnectionState.Ready));
            runtimeContext.PublishEndpoint(connection.RuntimeEndpoint);

            var connectionSlot = new RfLabPublishedConnectionSlot(
                connection,
                operationalConnectionFactory,
                timeProvider);
            var attachment = new RfLabPublishedAttachment(runtimeContext, connectionSlot);
            connection = null;
            return attachment;
        }
        catch (Exception primaryFailure)
        {
            if (connection is not null)
            {
                Exception? cleanupFailure = await CleanupAsync(connection).ConfigureAwait(false);
                if (cleanupFailure is not null)
                {
                    throw new AggregateException(
                        "The RF-Lab publication attempt and its cleanup both failed.",
                        primaryFailure,
                        cleanupFailure);
                }
            }

            throw;
        }
    }

    private async Task<Exception?> CleanupAsync(RfLabOperationalConnection connection)
    {
        List<Exception>? failures = null;

        try
        {
            connection.RuntimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(EndpointConnectionState.Disconnected));
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        try
        {
            runtimeContext.RemoveEndpoint(connection.RuntimeEndpoint);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        try
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        return failures switch
        {
            null => null,
            { Count: 1 } => failures[0],
            _ => new AggregateException(
                "The failed RF-Lab publication could not be cleaned up completely.",
                failures)
        };
    }
}
