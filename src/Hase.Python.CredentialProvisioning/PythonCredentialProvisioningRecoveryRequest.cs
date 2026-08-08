namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialProvisioningRecoveryRequest(
    string ProvisioningDirectory,
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath);
