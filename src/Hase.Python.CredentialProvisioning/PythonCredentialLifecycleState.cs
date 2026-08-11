namespace Hase.Python.CredentialProvisioning;

/// <summary>
/// Describes the time-based operational state of one selected Python client
/// credential. Enrollment, authorization, key matching, and trust custody are
/// validated before a state is returned.
/// </summary>
public enum PythonCredentialLifecycleState
{
    Active = 1,
    RotationDue = 2,
    Expiring = 3,
    Expired = 4,
    NotYetValid = 5,
}
