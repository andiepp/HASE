namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCommandExecutionAuthorizationException : Exception
{
    public PythonCommandExecutionAuthorizationException(string code)
        : base($"Python command authorization failed: {code}.") => Code = code;
    public string Code { get; }
}
