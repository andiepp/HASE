using Hase.CompactProtocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

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
    /// Creates a host configured for compact HASE endpoints reached through
    /// production System.IO.Ports serial transport.
    /// </summary>
    /// <param name="definitionRepository">
    /// Host repository containing exact compact endpoint definitions and
    /// their operational wire mappings.
    /// </param>
    /// <param name="reconnectPolicy">
    /// Retry-delay policy used for initial connection and recovery.
    /// </param>
    /// <param name="probeOptions">
    /// Compact endpoint health-probe timing, or <see langword="null"/> to use
    /// the approved default timing.
    /// </param>
    public static RuntimeEndpointAttachmentHost CreateCompactSerial(
        ICompactEndpointDefinitionRepository definitionRepository,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        CompactEndpointHealthProbeOptions? probeOptions = null)
    {
        ArgumentNullException.ThrowIfNull(
            definitionRepository);

        ArgumentNullException.ThrowIfNull(
            reconnectPolicy);

        var runtimeContext =
            new RuntimeContext();

        var attachmentService =
            new CompactSerialEndpointAttachmentService(
                runtimeContext,
                new SystemIoPortsSerialByteStreamFactory(),
                definitionRepository,
                reconnectPolicy,
                probeOptions
                ?? CompactEndpointHealthProbeOptions.Default);

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