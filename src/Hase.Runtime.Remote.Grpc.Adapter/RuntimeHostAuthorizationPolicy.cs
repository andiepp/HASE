namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Provides an immutable exact-match authorization policy for stable client
/// principals and semantic permissions.
/// </summary>
public sealed class RuntimeHostAuthorizationPolicy
{
    private readonly HashSet<RuntimeHostPermissionGrant> grants;

    /// <summary>
    /// Initializes one immutable authorization policy.
    /// </summary>
    public RuntimeHostAuthorizationPolicy(
        IEnumerable<RuntimeHostPermissionGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        this.grants =
            new HashSet<RuntimeHostPermissionGrant>(
                grants);
    }

    /// <summary>
    /// Determines whether one exact principal and permission combination is
    /// granted.
    /// </summary>
    public bool IsGranted(
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

        return grants.Contains(
            new RuntimeHostPermissionGrant(
                principalId,
                permission));
    }
}
