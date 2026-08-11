namespace Hase.Python.CredentialProvisioning;

public sealed record PythonCredentialRotationPreparationRequest(
    PythonCredentialLifecycleInspectionRequest Inspection,
    string ExpectedCurrentCredentialId,
    string ExpectedProfileSha256,
    string ExpectedEnrollmentSha256,
    string ExpectedAuthorizationPolicySha256,
    string ExpectedTrustedServerCertificateSha256);
