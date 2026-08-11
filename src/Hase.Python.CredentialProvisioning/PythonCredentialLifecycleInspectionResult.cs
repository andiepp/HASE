namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialLifecycleInspectionResult(
    PythonCredentialLifecycleState State,
    string CredentialId,
    string PrincipalId,
    string TrustPolicyId,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc,
    int RemainingWholeDays,
    IReadOnlyList<string> AuthorizationGrants,
    string ProfileSha256,
    string EnrollmentSha256,
    string AuthorizationPolicySha256,
    string TrustedServerCertificateSha256);
