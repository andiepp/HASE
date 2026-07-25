namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Grants one semantic northbound permission to one stable HASE client
/// principal identifier.
/// </summary>
public sealed record RuntimeHostPermissionGrant
{
    /// <summary>
    /// Initializes one immutable permission grant.
    /// </summary>
    public RuntimeHostPermissionGrant(
        string principalId,
        RuntimeHostPermission permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            principalId,
            nameof(principalId));

        if (string.IsNullOrWhiteSpace(permission.Value))
        {
            throw new ArgumentException(
                "The permission must have a non-empty value.",
                nameof(permission));
        }

        PrincipalId = principalId;
        Permission = permission;
    }

    /// <summary>
    /// Gets the stable HASE client-principal identifier.
    /// </summary>
    public string PrincipalId { get; }

    /// <summary>
    /// Gets the granted semantic permission.
    /// </summary>
    public RuntimeHostPermission Permission { get; }
}
