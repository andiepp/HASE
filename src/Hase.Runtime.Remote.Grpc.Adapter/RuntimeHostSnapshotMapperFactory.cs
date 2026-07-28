namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Composes the complete version 1 runtime-host snapshot mapper graph.
/// </summary>
public static class RuntimeHostSnapshotMapperFactory
{
    /// <summary>
    /// Creates a fully composed runtime-host snapshot mapper.
    /// </summary>
    public static RuntimeHostSnapshotMapper Create()
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

        var endpointSnapshotMapper =
            new RuntimeEndpointSnapshotMapper(
                endpointDescriptorMapper,
                new EndpointConnectionStatusMapper());

        return new RuntimeHostSnapshotMapper(
            endpointSnapshotMapper);
    }
}
