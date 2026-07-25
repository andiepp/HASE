namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Holds the fully composed version 1 runtime-host Property mapper roots.
/// </summary>
public sealed class RuntimeHostPropertyMappers
{
    /// <summary>
    /// Initializes the Property mapper roots.
    /// </summary>
    public RuntimeHostPropertyMappers(
        IRuntimeHostPropertyTargetMapper targetMapper,
        IRemoteValueMapper remoteValueMapper,
        IRuntimeHostCachedPropertyResultMapper cachedResultMapper,
        IRuntimeHostPropertyOperationResultMapper operationResultMapper)
    {
        TargetMapper =
            targetMapper
            ?? throw new ArgumentNullException(
                nameof(targetMapper));

        RemoteValueMapper =
            remoteValueMapper
            ?? throw new ArgumentNullException(
                nameof(remoteValueMapper));

        CachedResultMapper =
            cachedResultMapper
            ?? throw new ArgumentNullException(
                nameof(cachedResultMapper));

        OperationResultMapper =
            operationResultMapper
            ?? throw new ArgumentNullException(
                nameof(operationResultMapper));
    }

    /// <summary>
    /// Gets the inbound generation-scoped Property target mapper.
    /// </summary>
    public IRuntimeHostPropertyTargetMapper TargetMapper
    {
        get;
    }

    /// <summary>
    /// Gets the bidirectional normalized value mapper.
    /// </summary>
    public IRemoteValueMapper RemoteValueMapper
    {
        get;
    }

    /// <summary>
    /// Gets the cached Property result mapper.
    /// </summary>
    public IRuntimeHostCachedPropertyResultMapper CachedResultMapper
    {
        get;
    }

    /// <summary>
    /// Gets the authoritative Property operation result mapper.
    /// </summary>
    public IRuntimeHostPropertyOperationResultMapper OperationResultMapper
    {
        get;
    }
}
