namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Composes the complete version 1 runtime-host observation mapper graph.
/// </summary>
public static class RuntimeHostObservationMapperFactory
{
    /// <summary>
    /// Creates the fully composed observation mapper roots.
    /// </summary>
    public static RuntimeHostObservationMappers Create()
    {
        var quantityMapper =
            new QuantityMapper();
        var unitMapper =
            new UnitMapper(
                quantityMapper);
        var numericDataDescriptorMapper =
            new NumericDataDescriptorMapper(
                quantityMapper,
                unitMapper);
        var dataDescriptorMapper =
            new DataDescriptorMapper(
                numericDataDescriptorMapper);
        var propertyDescriptorMapper =
            new PropertyDescriptorMapper(
                dataDescriptorMapper);
        var instrumentDescriptorMapper =
            new InstrumentDescriptorMapper(
                propertyDescriptorMapper,
                new CommandDescriptorMapper(
                    dataDescriptorMapper),
                new EventDescriptorMapper());
        var endpointDescriptorMapper =
            new EndpointDescriptorMapper(
                instrumentDescriptorMapper);
        var connectionStatusMapper =
            new EndpointConnectionStatusMapper();
        var endpointSnapshotMapper =
            new RuntimeEndpointSnapshotMapper(
                endpointDescriptorMapper,
                connectionStatusMapper);
        var snapshotMapper =
            new RuntimeHostSnapshotMapper(
                endpointSnapshotMapper);
        var remoteValueMapper =
            new RemoteValueMapper();
        var propertyValueMapper =
            new PropertyValueMapper(
                remoteValueMapper);

        return new RuntimeHostObservationMappers(
            new ObservationInitialSnapshotMapper(
                snapshotMapper),
            new RuntimeHostObservationMapper(
                new RuntimeHostObservationKindMapper(),
                new RuntimeHostAttachmentObservationPayloadMapper(
                    endpointSnapshotMapper),
                new RuntimeHostConnectionStatusChangedObservationPayloadMapper(
                    connectionStatusMapper),
                new RuntimeHostPropertyValueChangedObservationPayloadMapper(
                    propertyValueMapper),
                new RuntimeHostEventOccurredObservationPayloadMapper(
                    remoteValueMapper)));
    }
}
