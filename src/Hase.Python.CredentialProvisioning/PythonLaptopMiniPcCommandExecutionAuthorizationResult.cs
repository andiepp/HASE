namespace Hase.Python.CredentialProvisioning;

public sealed record PythonLaptopMiniPcCommandExecutionAuthorizationResult(
    string TransactionId,
    string AuthorizationPolicySha256,
    string RollbackPath);
