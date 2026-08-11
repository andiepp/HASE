namespace Hase.Python.CredentialProvisioning;

public sealed record PythonLaptopMiniPcPropertyWriteAuthorizationRequest(
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    string RollbackPath);
