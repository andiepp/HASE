namespace Hase.Python.CredentialProvisioning;
public sealed record PythonObservationAuthorizationRequest(
    string AuthorizationPolicyPath, string ExpectedAuthorizationPolicySha256,
    string RollbackPath);
