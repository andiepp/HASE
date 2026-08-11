namespace Hase.Python.CredentialProvisioning;

public sealed record PythonLaptopMiniPcCommandExecutionAuthorizationRequest(
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    string RollbackPath);
