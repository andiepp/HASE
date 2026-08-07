namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Provides the exhaustive version 1 mapping from semantic remote operations
/// to authorization permissions.
/// </summary>
public sealed class RuntimeHostRemoteOperationPermissionMapper
    : IRuntimeHostRemoteOperationPermissionMapper
{
    /// <inheritdoc />
    public RuntimeHostPermission Map(
        RuntimeHostRemoteOperation operation)
    {
        return operation switch
        {
            RuntimeHostRemoteOperation.GetSnapshot =>
                RuntimeHostPermission.ReadSnapshot,

            RuntimeHostRemoteOperation.ReadCachedProperty =>
                RuntimeHostPermission.ReadCachedProperty,

            RuntimeHostRemoteOperation.ReadAuthoritativeProperty =>
                RuntimeHostPermission.ReadAuthoritativeProperty,

            RuntimeHostRemoteOperation.WriteProperty =>
                RuntimeHostPermission.WriteProperty,

            RuntimeHostRemoteOperation.ExecuteCommand =>
                RuntimeHostPermission.ExecuteCommand,

            RuntimeHostRemoteOperation.Observe =>
                RuntimeHostPermission.SubscribeObservation,

            RuntimeHostRemoteOperation.ObserveDiagnostics =>
                RuntimeHostPermission.SubscribeDiagnostics,

            RuntimeHostRemoteOperation.Unspecified =>
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "A specified remote operation is required."),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "The remote operation is not supported.")
        };
    }
}
