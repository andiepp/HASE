namespace Hase.Python.CredentialProvisioning;

public sealed record PythonLaptopMiniPcPropertyWriteAuthorizationResult(
    string TransactionId,
    string AuthorizationPolicySha256,
    string RollbackPath);
