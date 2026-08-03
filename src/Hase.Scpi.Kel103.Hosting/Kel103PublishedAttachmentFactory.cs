using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting;

/// <summary>
/// Opens, completely synchronizes, and then publishes one KEL-103 attachment.
/// </summary>
public sealed class Kel103PublishedAttachmentFactory
{
    private readonly RuntimeContext runtimeContext;
    private readonly Kel103OperationalConnectionFactory operationalConnectionFactory;

    public Kel103PublishedAttachmentFactory(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory,
        TimeProvider? timeProvider = null)
    {
        this.runtimeContext = runtimeContext
            ?? throw new ArgumentNullException(nameof(runtimeContext));
        operationalConnectionFactory = new Kel103OperationalConnectionFactory(
            runtimeContext,
            serialByteStreamFactory
                ?? throw new ArgumentNullException(nameof(serialByteStreamFactory)),
            timeProvider);
    }

    public async Task<Kel103PublishedAttachment> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        Kel103OperationalConnection? connection = null;
        try
        {
            connection = await operationalConnectionFactory
                .OpenAsync(endpointId, serialOptions, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            connection.RuntimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(EndpointConnectionState.Ready));
            runtimeContext.PublishEndpoint(connection.RuntimeEndpoint);

            var attachment = new Kel103PublishedAttachment(runtimeContext, connection);
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
                        "The KEL-103 publication attempt and its cleanup both failed.",
                        primaryFailure,
                        cleanupFailure);
                }
            }

            throw;
        }
    }

    private async Task<Exception?> CleanupAsync(Kel103OperationalConnection connection)
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
                "The failed KEL-103 publication could not be cleaned up completely.",
                failures)
        };
    }
}
