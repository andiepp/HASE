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
        CompactMappedEventNotificationSource eventSource,
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

        EventSource =
            eventSource;

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

    internal CompactEndpointDefinition Definition
    {
        get;
    }

    internal CompactPropertyMap PropertyMap
    {
        get;
    }

    internal CompactEventMap EventMap
    {
        get;
    }

    internal CompactEventNotificationResolver EventResolver
    {
        get;
    }

    /// <summary>
    /// Gets the current-connection-authoritative mapped compact event source.
    /// </summary>
    internal CompactMappedEventNotificationSource EventSource
    {
        get;
    }

    internal CompactRuntimeEndpointConnectionCoordinator Coordinator
    {
        get;
    }

    internal CompactRuntimeEndpointConnectionSupervisor Supervisor
    {
        get;
    }

    public EndpointConnectionSupervisionLifetime SupervisionLifetime
    {
        get;
    }

    public IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision
    {
        get;
    }

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

        var eventSource =
            new CompactMappedEventNotificationSource(
                eventResolver);

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
                new EndpointDescriptorCompatibilityValidator(),
                new CompactEndpointConnectionOwner(),
                eventSource,
                TimeProvider.System);

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
            eventSource,
            supervisionLifetime,
            coordinator,
            supervisor);
    }
}