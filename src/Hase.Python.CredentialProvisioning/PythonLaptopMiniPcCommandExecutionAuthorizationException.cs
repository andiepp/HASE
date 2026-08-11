namespace Hase.Python.CredentialProvisioning;

public sealed class PythonLaptopMiniPcCommandExecutionAuthorizationException
    : Exception
{
    public PythonLaptopMiniPcCommandExecutionAuthorizationException(string code)
        : base(code)
    {
        Code = code;
    }

    public string Code { get; }
}
