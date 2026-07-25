namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Composes the complete version 1 runtime-host Command mapper graph.
/// </summary>
public static class RuntimeHostCommandMapperFactory
{
    /// <summary>
    /// Creates the fully composed Command mapper roots.
    /// </summary>
    public static RuntimeHostCommandMappers Create()
    {
        var remoteValueMapper =
            new RemoteValueMapper();

        return new RuntimeHostCommandMappers(
            new RuntimeHostCommandTargetMapper(),
            remoteValueMapper,
            new RuntimeHostCommandOperationResultMapper(
                new RuntimeHostCommandOperationStatusMapper(),
                remoteValueMapper));
    }
}
