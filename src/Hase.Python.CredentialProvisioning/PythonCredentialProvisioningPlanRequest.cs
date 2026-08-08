namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialProvisioningPlanRequest(
    string SigningRootThumbprint,
    string CredentialId,
    string PrincipalId,
    string TrustPolicyId,
    string SourceProfilePath,
    string ProvisioningDirectory,
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    TimeSpan Validity,
    bool AllowReplacement = false);
