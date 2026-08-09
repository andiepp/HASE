namespace Hase.Python.CredentialProvisioning;

public sealed class PythonPropertyWriteAuthorizationException : Exception
{
    public PythonPropertyWriteAuthorizationException(string code)
        : base($"Python Property-write authorization failed: {code}.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
