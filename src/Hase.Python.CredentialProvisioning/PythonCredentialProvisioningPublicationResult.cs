namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialProvisioningPublicationResult(
    string TransactionId,
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath,
    bool ReplacedCredentialOutputs);
