namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioningException : Exception
{
    public PythonCredentialProvisioningException(
        string code)
        : base($"Python credential provisioning failed: {code}.")
    {
        Code = code;
    }

    public string Code { get; }
}
