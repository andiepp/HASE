namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCrossComputerRotationBeginRequest(
    string RotationRequestPath,
    string ProfileTemplatePath,
    string EnrollmentPath,
    string AuthorizationPolicyPath,
    string ProvisioningDirectory,
    string TransferArchivePath,
    string SigningRootThumbprint,
    string TrustPolicyId,
    TimeSpan Validity,
    string ExpectedEnrollmentSha256,
    string ExpectedAuthorizationPolicySha256);
