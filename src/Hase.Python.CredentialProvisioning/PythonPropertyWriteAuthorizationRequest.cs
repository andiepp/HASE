namespace Hase.Python.CredentialProvisioning;

public sealed record PythonPropertyWriteAuthorizationRequest(
    string AuthorizationPolicyPath,
    string ExpectedAuthorizationPolicySha256,
    string ApplicationProfilePath,
    string ExpectedApplicationProfileSha256,
    string PolicyRollbackPath,
    string ProfileRollbackPath);
