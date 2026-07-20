using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Attaches native HASE endpoints reached through framed TCP to one runtime
/// context.
/// </summary>
public sealed class NativeNetworkEndpointAttachmentService
    : IEndpointAttachmentService
{
    private readonly NativeEndpointAttachmentSuccessfulPath
        _successfulPath;

    private readonly Func<
        NetworkEndpointConnectionDefinition,
        CancellationToken,
        Task<NativeEndpointBootstrapResult>>
        _bootstrapAsync;

    private readonly Func<
        NetworkEndpointConnectionDefinition,
        RuntimeEndpoint,
        INativeEndpointOperationalResources>
        _createOperationalResources;

    /// <summary>
    /// Initializes a native network endpoint attachment service.
    /// </summary>
    public NativeNetworkEndpointAttachmentService(
        RuntimeContext runtimeContext,
        INativeEndpointBootstrapper bootstrapper,
        IRuntimeEndpointSynchronizer synchronizer,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        int maximumPayloadLength =
            TcpNativeEndpointBootstrapClient.DefaultMaximumPayloadLength)
        : this(
            runtimeContext,
            new TcpNativeEndpointBootstrapClient(
                bootstrapper,
                maximumPayloadLength)
                .BootstrapAsync,
            (
                connectionDefinition,
                runtimeEndpoint) =>
                NativeEndpointOperationalResources.CreateNetwork(
                    connectionDefinition,
                    runtimeEndpoint,
                    synchronizer,
                    reconnectPolicy,
                    maximumPayloadLength))
    {
        ArgumentNullException.ThrowIfNull(
            synchronizer);

        ArgumentNullException.ThrowIfNull(
            reconnectPolicy);
    }

    internal NativeNetworkEndpointAttachmentService(
        RuntimeContext runtimeContext,
        Func<
            NetworkEndpointConnectionDefinition,
            CancellationToken,
            Task<NativeEndpointBootstrapResult>>
            bootstrapAsync,
        Func<
            NetworkEndpointConnectionDefinition,
            RuntimeEndpoint,
            INativeEndpointOperationalResources>
            createOperationalResources)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeContext);

        _bootstrapAsync =
            bootstrapAsync
            ?? throw new ArgumentNullException(
                nameof(bootstrapAsync));

        _createOperationalResources =
            createOperationalResources
            ?? throw new ArgumentNullException(
                nameof(createOperationalResources));

        _successfulPath =
            new NativeEndpointAttachmentSuccessfulPath(
                runtimeContext);
    }

    /// <inheritdoc />
    public async Task<IEndpointAttachmentSession> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.DescriptorSource
            is not EndpointProvidedDescriptorSource)
        {
            throw new NotSupportedException(
                "Native network attachment currently requires the "
                + "endpoint-provided descriptor source.");
        }

        if (request.ConnectionDefinition
            is not NetworkEndpointConnectionDefinition
                connectionDefinition)
        {
            throw new NotSupportedException(
                "Native network attachment requires a framed-TCP "
                + "network connection definition.");
        }

        NativeEndpointBootstrapResult bootstrapResult =
            await _bootstrapAsync(
                connectionDefinition,
                cancellationToken);

        return await _successfulPath.CompleteAsync(
            request,
            bootstrapResult,
            runtimeEndpoint =>
                _createOperationalResources(
                    connectionDefinition,
                    runtimeEndpoint),
            cancellationToken);
    }
}