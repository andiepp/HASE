namespace Hase.Python.CredentialProvisioning;

public sealed class PythonLaptopMiniPcPropertyWriteAuthorizationException
    : Exception
{
    public PythonLaptopMiniPcPropertyWriteAuthorizationException(string code)
        : base(code)
    {
        Code = code;
    }

    public string Code { get; }
}
