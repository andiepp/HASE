namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialProvisioningRequest(
    string ProvisioningDirectory,
    string SourceProfilePath,
    string CertificatePath,
    string PrivateKeyPath,
    string ProfilePath,
    bool AllowReplacement = false);
