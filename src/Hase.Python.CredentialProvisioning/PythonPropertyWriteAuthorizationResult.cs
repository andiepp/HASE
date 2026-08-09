namespace Hase.Python.CredentialProvisioning;

public sealed record PythonPropertyWriteAuthorizationResult(
    string TransactionId,
    string AuthorizationPolicySha256,
    string ApplicationProfileSha256,
    string PolicyRollbackPath,
    string ProfileRollbackPath);
