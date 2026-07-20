using Hase.Runtime.Runtime;
using Hase.Runtime.Connections;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Owns the endpoint attachment inventory for one runtime context.
/// </summary>
/// <remarks>
/// The supplied attachment service must attach endpoints to the supplied
/// runtime context. Disposing the host disposes the complete attachment
/// inventory and therefore every attachment session owned by it.
/// </remarks>
public sealed class RuntimeEndpointAttachmentHost
    : IAsyncDisposable
{
    /// <summary>
    /// Creates a host configured for native HASE endpoints reached through
    /// framed TCP.
    /// </summary>
    public static RuntimeEndpointAttachmentHost CreateNativeNetwork(
        INativeEndpointBootstrapper bootstrapper,
        IRuntimeEndpointSynchronizer synchronizer,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        int maximumPayloadLength =
            TcpNativeEndpointBootstrapClient.DefaultMaximumPayloadLength)
    {
        ArgumentNullException.ThrowIfNull(
            bootstrapper);

        ArgumentNullException.ThrowIfNull(
            synchronizer);

        ArgumentNullException.ThrowIfNull(
            reconnectPolicy);

        var runtimeContext =
            new RuntimeContext();

        var attachmentService =
            new NativeNetworkEndpointAttachmentService(
                runtimeContext,
                bootstrapper,
                synchronizer,
                reconnectPolicy,
                maximumPayloadLength);

        return new RuntimeEndpointAttachmentHost(
            runtimeContext,
            attachmentService);
    }

    /// <summary>
    /// Initializes the endpoint attachment owner for one runtime context.
    /// </summary>
    public RuntimeEndpointAttachmentHost(
        RuntimeContext runtimeContext,
        IEndpointAttachmentService attachmentService)
    {
        RuntimeContext =
            runtimeContext
            ?? throw new ArgumentNullException(
                nameof(runtimeContext));

        ArgumentNullException.ThrowIfNull(
            attachmentService);

        AttachmentInventory =
            new RuntimeEndpointAttachmentInventory(
                attachmentService);
    }

    /// <summary>
    /// Gets the runtime context whose endpoint lifecycle is owned by this
    /// host.
    /// </summary>
    public RuntimeContext RuntimeContext
    {
        get;
    }

    /// <summary>
    /// Gets the host-owned endpoint attachment inventory.
    /// </summary>
    public IRuntimeEndpointAttachmentInventory AttachmentInventory
    {
        get;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return AttachmentInventory.DisposeAsync();
    }
}