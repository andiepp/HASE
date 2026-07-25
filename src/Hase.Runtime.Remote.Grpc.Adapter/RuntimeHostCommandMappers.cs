namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Holds the fully composed version 1 runtime-host Command mapper roots.
/// </summary>
public sealed class RuntimeHostCommandMappers
{
    /// <summary>
    /// Initializes the Command mapper roots.
    /// </summary>
    public RuntimeHostCommandMappers(
        IRuntimeHostCommandTargetMapper targetMapper,
        IRemoteValueMapper remoteValueMapper,
        IRuntimeHostCommandOperationResultMapper operationResultMapper)
    {
        TargetMapper =
            targetMapper
            ?? throw new ArgumentNullException(
                nameof(targetMapper));

        RemoteValueMapper =
            remoteValueMapper
            ?? throw new ArgumentNullException(
                nameof(remoteValueMapper));

        OperationResultMapper =
            operationResultMapper
            ?? throw new ArgumentNullException(
                nameof(operationResultMapper));
    }

    /// <summary>
    /// Gets the inbound generation-scoped Command target mapper.
    /// </summary>
    public IRuntimeHostCommandTargetMapper TargetMapper
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
    /// Gets the Command operation result mapper.
    /// </summary>
    public IRuntimeHostCommandOperationResultMapper OperationResultMapper
    {
        get;
    }
}
