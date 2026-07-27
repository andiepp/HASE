namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Defines the external inputs used to provision one client enrollment.
/// </summary>
internal sealed record ProvisionClientEnrollmentArguments
{
    /// <summary>
    /// Initializes one validated provisioning request.
    /// </summary>
    public ProvisionClientEnrollmentArguments(
        string publicCertificateFilePath,
        string enrollmentFilePath,
        string principalId,
        string trustPolicyId)
    {
        PublicCertificateFilePath =
            RequireFullyQualifiedPath(
                publicCertificateFilePath,
                nameof(publicCertificateFilePath));
        EnrollmentFilePath =
            RequireFullyQualifiedPath(
                enrollmentFilePath,
                nameof(enrollmentFilePath));
        ArgumentException.ThrowIfNullOrWhiteSpace(
            principalId,
            nameof(principalId));
        ArgumentException.ThrowIfNullOrWhiteSpace(
            trustPolicyId,
            nameof(trustPolicyId));

        PrincipalId =
            principalId;
        TrustPolicyId =
            trustPolicyId;
    }

    public string PublicCertificateFilePath
    {
        get;
    }

    public string EnrollmentFilePath
    {
        get;
    }

    public string PrincipalId
    {
        get;
    }

    public string TrustPolicyId
    {
        get;
    }

    private static string RequireFullyQualifiedPath(
        string filePath,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(
            filePath,
            parameterName);

        if (string.IsNullOrWhiteSpace(
                filePath)
            || !Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                "The provisioning file path must be fully qualified.",
                parameterName);
        }

        return Path.GetFullPath(
            filePath);
    }
}
