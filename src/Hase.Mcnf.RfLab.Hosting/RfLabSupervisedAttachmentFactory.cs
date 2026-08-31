using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.Mcnf.RfLab.Hosting;

/// <summary>
/// Creates a published RF-Lab attachment with automatic fault recovery.
/// </summary>
public sealed class RfLabSupervisedAttachmentFactory
{
    private readonly RfLabPublishedAttachmentFactory publishedAttachmentFactory;
    private readonly IRuntimeEndpointReconnectPolicy reconnectPolicy;
    private readonly TimeProvider timeProvider;

    public RfLabSupervisedAttachmentFactory(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory,
        IRuntimeEndpointReconnectPolicy? reconnectPolicy = null,
        TimeSpan? settleDelay = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        ArgumentNullException.ThrowIfNull(serialByteStreamFactory);
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.reconnectPolicy = reconnectPolicy ?? new DefaultRuntimeEndpointReconnectPolicy();
        publishedAttachmentFactory = new RfLabPublishedAttachmentFactory(
            runtimeContext,
            serialByteStreamFactory,
            settleDelay,
            this.timeProvider);
    }

    public async Task<RfLabSupervisedAttachment> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
        => await OpenAsync(
            endpointId,
            RfLabReadOnlyDefinition.EndpointDefinition,
            serialOptions,
            cancellationToken).ConfigureAwait(false);

    public async Task<RfLabSupervisedAttachment> OpenAsync(
        EndpointId endpointId,
        EndpointDescriptorDefinition definition,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        RfLabPublishedAttachment? publishedAttachment = await publishedAttachmentFactory
            .OpenAsync(endpointId, definition, serialOptions, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var supervisor = new RfLabPublishedAttachmentSupervisor(
                publishedAttachment,
                serialOptions,
                CreateRecoveryPolicy(
                    publishedAttachment.RuntimeEndpoint,
                    reconnectPolicy),
                timeProvider);
            var supervisionLifetime = new EndpointConnectionSupervisionLifetime(
                supervisor.RunAsync);
            var passiveHealthMonitor = new RfLabPassiveHealthMonitor(
                publishedAttachment,
                timeProvider);
            var passiveHealthLifetime = new EndpointConnectionSupervisionLifetime(
                passiveHealthMonitor.RunAsync);
            _ = supervisionLifetime.RunAsync();
            _ = passiveHealthLifetime.RunAsync();

            var attachment = new RfLabSupervisedAttachment(
                publishedAttachment,
                supervisionLifetime,
                passiveHealthLifetime,
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
                        "The supervised RF-Lab attachment creation and cleanup both failed.",
                        primaryFailure,
                        cleanupFailure);
                }
            }

            throw;
        }
    }

    internal static IRuntimeEndpointReconnectPolicy CreateRecoveryPolicy(
        RuntimeEndpoint runtimeEndpoint,
        IRuntimeEndpointReconnectPolicy reconnectPolicy)
    {
        ArgumentNullException.ThrowIfNull(runtimeEndpoint);
        ArgumentNullException.ThrowIfNull(reconnectPolicy);
        return new RuntimeEndpointReconnectDiagnosticPolicy(
            reconnectPolicy,
            runtimeEndpoint.Context.Diagnostics,
            runtimeEndpoint.Descriptor.Id.Value);
    }
}
