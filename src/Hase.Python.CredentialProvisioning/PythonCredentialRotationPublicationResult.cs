namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialRotationPublicationResult(
    string TransactionId,
    string Disposition,
    bool RollbackRetained);
