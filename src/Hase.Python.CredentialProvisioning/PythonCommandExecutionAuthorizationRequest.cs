namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCommandExecutionAuthorizationRequest(
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    string RollbackPath);
