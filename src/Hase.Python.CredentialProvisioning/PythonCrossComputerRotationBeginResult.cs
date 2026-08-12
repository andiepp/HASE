namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCrossComputerRotationBeginResult(
    string TransactionId,
    string Disposition,
    bool RollbackRetained,
    bool TransferArchiveCreated);
