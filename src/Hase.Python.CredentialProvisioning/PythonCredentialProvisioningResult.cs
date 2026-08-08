namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialProvisioningResult(
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    string CredentialId,
    bool ReplacedExistingFiles);
