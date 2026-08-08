namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioningPlanException : Exception
{
    public PythonCredentialProvisioningPlanException(string code)
        : base($"Python credential provisioning plan failed: {code}.")
    {
        Code = code;
    }

    public string Code { get; }
}
