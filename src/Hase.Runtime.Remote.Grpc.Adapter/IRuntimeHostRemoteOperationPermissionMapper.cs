namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one semantic northbound remote operation to the explicit permission
/// required by ADR-0031.
/// </summary>
public interface IRuntimeHostRemoteOperationPermissionMapper
{
    /// <summary>
    /// Maps one specified remote operation to its required permission.
    /// </summary>
    RuntimeHostPermission Map(
        RuntimeHostRemoteOperation operation);
}
