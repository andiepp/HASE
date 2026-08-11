namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialRotationPublicationRequest(
    string ProvisioningDirectory,
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath,
    string ExpectedCertificateSha256,
    string ExpectedPrivateKeySha256,
    string ExpectedProfileSha256,
    string ExpectedEnrollmentSha256,
    string ExpectedAuthorizationPolicySha256);
