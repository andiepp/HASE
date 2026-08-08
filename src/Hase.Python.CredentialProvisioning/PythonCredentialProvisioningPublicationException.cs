namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioningPublicationException : Exception
{
    public PythonCredentialProvisioningPublicationException(string code)
        : base($"Python credential publication failed: {code}.")
    {
        Code = code;
    }

    public string Code { get; }
}
