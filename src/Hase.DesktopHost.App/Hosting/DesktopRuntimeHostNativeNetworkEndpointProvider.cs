using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.Hosting;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;

namespace Hase.DesktopHost.App.Hosting;

/// <summary>
/// Supplies the native-protocol endpoints this host reaches over the network.
/// </summary>
/// <remarks>
/// The kind carries no instrument knowledge: it attaches whatever native
/// endpoint answers at the configured address and publishes the descriptor
/// that endpoint provides.
/// </remarks>
public sealed class DesktopRuntimeHostNativeNetworkEndpointProvider
    : IDesktopRuntimeHostEndpointProvider
{
    /// <summary>
    /// The identifier this provider is registered under.
    /// </summary>
    public const string Id = "native-network";

    /// <summary>
    /// The endpoint kind reported in host diagnostics.
    /// </summary>
    public const string EndpointKind = "NativeNetwork";

    /// <inheritdoc />
    public string ProviderId => Id;

    /// <inheritdoc />
    public bool Supports(IEndpointConnectionDefinition connectionDefinition)
    {
        ArgumentNullException.ThrowIfNull(connectionDefinition);

        return connectionDefinition is NetworkEndpointConnectionDefinition;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The runtime attachment host routes native network definitions itself,
    /// so this provider contributes no service of its own.
    /// </remarks>
    public IEndpointAttachmentService? CreateAttachmentService(
        RuntimeContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);

        return null;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>
        ResolveAttachmentsAsync(
            DesktopRuntimeHostEndpointProviderContext context,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var attachments = new List<DesktopRuntimeHostEndpointAttachment>();

        foreach (DesktopRuntimeHostEndpointEntry entry
            in context.EndpointComposition.ForProvider(Id))
        {
            var endpoint = new DesktopRuntimeHostNativeNetworkEndpointProfile(
                entry.ExpectedEndpointId,
                entry.RequireString("host"),
                entry.RequireInt32("port"));

            attachments.Add(
                new DesktopRuntimeHostEndpointAttachment(
                    endpoint.ExpectedEndpointId,
                    EndpointKind,
                    (inventory, token) => AttachAsync(
                        inventory,
                        endpoint,
                        token)));
        }

        return Task.FromResult<
            IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>(attachments);
    }

    private static async Task AttachAsync(
        IRuntimeEndpointAttachmentInventory attachmentInventory,
        DesktopRuntimeHostNativeNetworkEndpointProfile endpoint,
        CancellationToken cancellationToken)
    {
        var request =
            new EndpointAttachmentRequest(
                NetworkEndpointConnectionDefinition.FromConfiguration(
                    new TcpTransportOptions(
                        endpoint.Host,
                        endpoint.Port),
                    new EndpointId(
                        endpoint.ExpectedEndpointId)),
                EndpointProvidedDescriptorSource.Instance);

        await attachmentInventory.AttachAsync(
            request,
            cancellationToken);
    }
}
