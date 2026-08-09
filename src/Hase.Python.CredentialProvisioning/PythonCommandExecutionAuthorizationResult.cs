namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCommandExecutionAuthorizationResult(
    string TransactionId,
    string AuthorizationPolicySha256,
    string RollbackPath);
