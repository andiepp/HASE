namespace Hase.Python.CredentialProvisioning;
public sealed record PythonObservationAuthorizationResult(
    string TransactionId, string AuthorizationPolicySha256, string RollbackPath);
