namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Composes the complete version 1 runtime-host Property mapper graph.
/// </summary>
public static class RuntimeHostPropertyMapperFactory
{
    /// <summary>
    /// Creates the fully composed Property mapper roots.
    /// </summary>
    public static RuntimeHostPropertyMappers Create()
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

        var remoteValueMapper =
            new RemoteValueMapper();

        var propertyValueMapper =
            new PropertyValueMapper(
                remoteValueMapper);

        var publishedSnapshotMapper =
            new PublishedRuntimePropertySnapshotMapper(
                propertyDescriptorMapper,
                new EndpointConnectionStatusMapper(),
                propertyValueMapper);

        var statusMapper =
            new RuntimeHostPropertyOperationStatusMapper();

        return new RuntimeHostPropertyMappers(
            new RuntimeHostPropertyTargetMapper(),
            new RuntimeHostCachedPropertyResultMapper(
                statusMapper,
                publishedSnapshotMapper),
            new RuntimeHostPropertyOperationResultMapper(
                statusMapper,
                propertyValueMapper));
    }
}
