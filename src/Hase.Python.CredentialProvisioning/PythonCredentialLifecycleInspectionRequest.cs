namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialLifecycleInspectionRequest(
    string ProfilePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath,
    string ExpectedPrincipalId,
    string ExpectedTrustPolicyId,
    IReadOnlyList<string> ExpectedAuthorizationGrants);
