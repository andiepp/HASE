using Hase.CompactProtocol;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Contains the operational connection resources created for one compact
/// serial endpoint after authoritative bootstrap.
/// </summary>
internal sealed class CompactEndpointOperationalResources
    : ICompactEndpointOperationalResources
{
    private CompactEndpointOperationalResources(
        CompactEndpointDefinition definition,
        CompactPropertyMap propertyMap,
        CompactEventMap eventMap,
        CompactEventNotificationResolver eventResolver,
        EndpointConnectionSupervisionLifetime supervisionLifetime,
        CompactRuntimeEndpointConnectionCoordinator coordinator,
        CompactRuntimeEndpointConnectionSupervisor supervisor)
    {
        Definition =
            definition;

        PropertyMap =
            propertyMap;

        EventMap =
            eventMap;

        EventResolver =
            eventResolver;

        SupervisionLifetime =
            supervisionLifetime;

        Coordinator =
            coordinator;

        Supervisor =
            supervisor;

        ResourcesAfterSupervision =
        [
            coordinator
        ];
    }

    /// <summary>
    /// Gets the exact compact definition used to create the operational
    /// resource graph.
    /// </summary>
    internal CompactEndpointDefinition Definition
    {
        get;
    }

    /// <summary>
    /// Gets the validated compact property map.
    /// </summary>
    internal CompactPropertyMap PropertyMap
    {
        get;
    }

    /// <summary>
    /// Gets the validated compact event map.
    /// </summary>
    internal CompactEventMap EventMap
    {
        get;
    }

    /// <summary>
    /// Gets the descriptor-bound compact event-notification resolver.
    /// </summary>
    internal CompactEventNotificationResolver EventResolver
    {
        get;
    }

    /// <summary>
    /// Gets the compact runtime endpoint connection coordinator.
    /// </summary>
    internal CompactRuntimeEndpointConnectionCoordinator Coordinator
    {
        get;
    }

    /// <summary>
    /// Gets the compact runtime endpoint connection supervisor.
    /// </summary>
    internal CompactRuntimeEndpointConnectionSupervisor Supervisor
    {
        get;
    }

    /// <inheritdoc />
    public EndpointConnectionSupervisionLifetime SupervisionLifetime
    {
        get;
    }

    /// <inheritdoc />
    public IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision
    {
        get;
    }

    /// <summary>
    /// Constructs the complete operational connection graph for one compact
    /// endpoint reached through serial transport.
    /// </summary>
    internal static CompactEndpointOperationalResources CreateSerial(
        SerialEndpointConnectionDefinition connectionDefinition,
        CompactEndpointDefinition definition,
        RuntimeEndpoint runtimeEndpoint,
        ISerialByteStreamFactory serialByteStreamFactory,
        ICompactEndpointDefinitionRepository definitionRepository,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        CompactEndpointHealthProbeOptions probeOptions)
    {
        ArgumentNullException.ThrowIfNull(
            connectionDefinition);

        ArgumentNullException.ThrowIfNull(
            definition);

        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        ArgumentNullException.ThrowIfNull(
            serialByteStreamFactory);

        ArgumentNullException.ThrowIfNull(
            definitionRepository);

        ArgumentNullException.ThrowIfNull(
            reconnectPolicy);

        ArgumentNullException.ThrowIfNull(
            probeOptions);

        CompactPropertyMap propertyMap =
            definition.CreatePropertyMap();

        CompactEventMap eventMap =
            definition.CreateEventMap();

        var eventResolver =
            new CompactEventNotificationResolver(
                eventMap);

        var descriptorRepository =
            new CompactEndpointDescriptorRepositoryAdapter(
                definitionRepository);

        var connectionFactory =
            new CompactSerialEndpointConnector(
                serialByteStreamFactory,
                descriptorRepository);

        var coordinator =
            new CompactRuntimeEndpointConnectionCoordinator(
                connectionFactory,
                connectionDefinition.TransportOptions,
                propertyMap,
                runtimeEndpoint,
                new EndpointDescriptorCompatibilityValidator());

        var supervisor =
            new CompactRuntimeEndpointConnectionSupervisor(
                coordinator,
                propertyMap,
                reconnectPolicy,
                probeOptions);

        var supervisionLifetime =
            new EndpointConnectionSupervisionLifetime(
                supervisor.RunAsync);

        return new CompactEndpointOperationalResources(
            definition,
            propertyMap,
            eventMap,
            eventResolver,
            supervisionLifetime,
            coordinator,
            supervisor);
    }
}