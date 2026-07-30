using Hase.Runtime.Runtime;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Contains the operational connection resources created for one native
/// network endpoint after bootstrap.
/// </summary>
internal sealed class NativeEndpointOperationalResources
    : INativeEndpointOperationalResources
{
    private NativeEndpointOperationalResources(
        EndpointConnectionSupervisionLifetime supervisionLifetime,
        RuntimeEndpointConnectionCoordinator coordinator,
        RuntimeEndpointConnectionSupervisor supervisor,
        TransportConnectionManager connectionManager)
    {
        SupervisionLifetime =
            supervisionLifetime;

        Coordinator =
            coordinator;

        Supervisor =
            supervisor;

        PropertyOperations =
            new NativeEndpointAttachmentPropertyOperations(
                coordinator,
                NativeEndpointAttachmentPropertyOperations
                    .DefaultOperationTimeout);

        CommandOperations =
            new NativeEndpointAttachmentCommandOperations(
                coordinator,
                NativeEndpointAttachmentCommandOperations
                    .DefaultOperationTimeout);

        ConnectionManager =
            connectionManager;

        ResourcesAfterSupervision =
        [
            coordinator,
            connectionManager
        ];
    }

    /// <summary>
    /// Gets the lifetime that starts and stops connection supervision.
    /// </summary>
    public EndpointConnectionSupervisionLifetime SupervisionLifetime
    {
        get;
    }

    /// <summary>
    /// Gets the coordinator that owns runtime protocol bindings.
    /// </summary>
    internal RuntimeEndpointConnectionCoordinator Coordinator
    {
        get;
    }

    /// <summary>
    /// Gets the supervisor that owns native connection recovery.
    /// </summary>
    internal RuntimeEndpointConnectionSupervisor Supervisor
    {
        get;
    }

    /// <summary>
    /// Gets the transport-independent Property port bound to this attachment's
    /// native coordinator.
    /// </summary>
    public IEndpointAttachmentPropertyOperations PropertyOperations
    {
        get;
    }

    /// <summary>
    /// Gets the transport-independent Command port bound to this attachment's
    /// native coordinator.
    /// </summary>
    public IEndpointAttachmentCommandOperations CommandOperations
    {
        get;
    }

    /// <summary>
    /// Gets the manager that owns the operational transport connection.
    /// </summary>
    internal TransportConnectionManager ConnectionManager
    {
        get;
    }

    /// <summary>
    /// Gets resources in the order required after supervision has stopped.
    /// </summary>
    public IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision
    {
        get;
    }

    /// <summary>
    /// Constructs the complete operational connection graph for a native
    /// endpoint reached through framed TCP.
    /// </summary>
    internal static NativeEndpointOperationalResources CreateNetwork(
        NetworkEndpointConnectionDefinition connectionDefinition,
        RuntimeEndpoint runtimeEndpoint,
        IRuntimeEndpointSynchronizer synchronizer,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        int maximumPayloadLength =
            TcpNativeEndpointBootstrapClient.DefaultMaximumPayloadLength)
    {
        ArgumentNullException.ThrowIfNull(
            connectionDefinition);

        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        ArgumentNullException.ThrowIfNull(
            synchronizer);

        ArgumentNullException.ThrowIfNull(
            reconnectPolicy);

        ITransportFactory transportFactory =
            new TcpTransportFactory(
                connectionDefinition.TransportOptions,
                maximumPayloadLength);

        var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        var identityValidatingSynchronizer =
            new IdentityValidatingRuntimeEndpointSynchronizer(
                synchronizer);

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                identityValidatingSynchronizer);

        var diagnosticReconnectPolicy =
            new RuntimeEndpointReconnectDiagnosticPolicy(
                reconnectPolicy,
                runtimeEndpoint.Context.Diagnostics,
                runtimeEndpoint.Descriptor.Id.Value);

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                diagnosticReconnectPolicy);

        var supervisionLifetime =
            new EndpointConnectionSupervisionLifetime(
                supervisor.RunAsync);

        return new NativeEndpointOperationalResources(
            supervisionLifetime,
            coordinator,
            supervisor,
            connectionManager);
    }
}
