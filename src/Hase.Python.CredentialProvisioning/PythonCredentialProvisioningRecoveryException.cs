namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioningRecoveryException : Exception
{
    public PythonCredentialProvisioningRecoveryException(string code)
        : base($"Python credential recovery failed: {code}.")
    {
        Code = code;
    }

    public string Code { get; }
}
