namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialProvisioningPlan(
    string PlanId,
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
    string AuthorizationPolicySha256,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc,
    int LeafRsaKeySize,
    string LeafSignatureAlgorithm,
    IReadOnlyList<string> LeafEnhancedKeyUsages,
    IReadOnlyList<string> AuthorizationGrants,
    bool AllowReplacement);
