namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialProvisioningRecoveryResult(
    PythonCredentialProvisioningRecoveryDisposition Disposition,
    string? TransactionId);

public enum PythonCredentialProvisioningRecoveryDisposition
{
    NoTransaction = 0,
    RolledBack = 1,
    CommittedCleanupCompleted = 2,
}
