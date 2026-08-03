using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting;

/// <summary>
/// Creates a published KEL-103 attachment with automatic fault recovery.
/// </summary>
public sealed class Kel103SupervisedAttachmentFactory
{
    private readonly Kel103PublishedAttachmentFactory publishedAttachmentFactory;
    private readonly IRuntimeEndpointReconnectPolicy reconnectPolicy;
    private readonly TimeProvider timeProvider;

    public Kel103SupervisedAttachmentFactory(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory,
        IRuntimeEndpointReconnectPolicy? reconnectPolicy = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        ArgumentNullException.ThrowIfNull(serialByteStreamFactory);
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.reconnectPolicy = reconnectPolicy ?? new DefaultRuntimeEndpointReconnectPolicy();
        publishedAttachmentFactory = new Kel103PublishedAttachmentFactory(
            runtimeContext,
            serialByteStreamFactory,
            this.timeProvider);
    }

    public async Task<Kel103SupervisedAttachment> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        Kel103PublishedAttachment? publishedAttachment = await publishedAttachmentFactory
            .OpenAsync(endpointId, serialOptions, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var supervisor = new Kel103PublishedAttachmentSupervisor(
                publishedAttachment,
                serialOptions,
                reconnectPolicy,
                timeProvider);
            var supervisionLifetime = new EndpointConnectionSupervisionLifetime(
                supervisor.RunAsync);
            _ = supervisionLifetime.RunAsync();

            var attachment = new Kel103SupervisedAttachment(
                publishedAttachment,
                supervisionLifetime,
                supervisor);
            publishedAttachment = null;
            return attachment;
        }
        catch (Exception primaryFailure)
        {
            if (publishedAttachment is not null)
            {
                try
                {
                    await publishedAttachment.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        "The supervised KEL-103 attachment creation and cleanup both failed.",
                        primaryFailure,
                        cleanupFailure);
                }
            }

            throw;
        }
    }
}
