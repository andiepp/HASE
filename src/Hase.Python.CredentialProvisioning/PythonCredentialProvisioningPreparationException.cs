namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioningPreparationException : Exception
{
    public PythonCredentialProvisioningPreparationException(string code)
        : base($"Python credential preparation failed: {code}.")
    {
        Code = code;
    }

    public string Code { get; }
}
