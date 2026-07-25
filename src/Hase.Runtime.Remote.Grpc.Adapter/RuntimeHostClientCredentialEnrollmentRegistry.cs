namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Provides deterministic in-memory lookup of explicitly enrolled client
/// credentials.
/// </summary>
public sealed class RuntimeHostClientCredentialEnrollmentRegistry
    : IRuntimeHostClientCredentialEnrollmentRegistry
{
    private readonly IReadOnlyDictionary<
        RuntimeHostClientCredentialIdentity,
        RuntimeHostClientCredentialEnrollment> enrollments;

    /// <summary>
    /// Initializes the registry from the complete enrollment set.
    /// </summary>
    public RuntimeHostClientCredentialEnrollmentRegistry(
        IEnumerable<RuntimeHostClientCredentialEnrollment> enrollments)
    {
        ArgumentNullException.ThrowIfNull(
            enrollments);

        Dictionary<
            RuntimeHostClientCredentialIdentity,
            RuntimeHostClientCredentialEnrollment> enrollmentMap =
                [];

        foreach (RuntimeHostClientCredentialEnrollment enrollment
            in enrollments)
        {
            if (enrollment is null)
            {
                throw new ArgumentException(
                    "The enrollment collection must not contain null.",
                    nameof(enrollments));
            }

            if (!enrollmentMap.TryAdd(
                enrollment.CredentialIdentity,
                enrollment))
            {
                throw new ArgumentException(
                    "The enrollment collection contains a duplicate "
                    + $"credential identity '{enrollment.CredentialIdentity}'.",
                    nameof(enrollments));
            }
        }

        this.enrollments = enrollmentMap;
    }

    /// <inheritdoc />
    public bool TryResolve(
        RuntimeHostClientCredentialIdentity credentialIdentity,
        DateTimeOffset authenticatedAtUtc,
        out RuntimeHostClientPrincipal? principal)
    {
        if (credentialIdentity == default)
        {
            throw new ArgumentException(
                "The client-credential identity must be specified.",
                nameof(credentialIdentity));
        }

        if (authenticatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The authentication timestamp must use UTC.",
                nameof(authenticatedAtUtc));
        }

        if (!enrollments.TryGetValue(
            credentialIdentity,
            out RuntimeHostClientCredentialEnrollment? enrollment))
        {
            principal = null;
            return false;
        }

        principal = enrollment.CreatePrincipal(
            authenticatedAtUtc);
        return true;
    }
}
