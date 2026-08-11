namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialLifecycleInspectionException : Exception
{
    public PythonCredentialLifecycleInspectionException(string code)
        : base("Python credential lifecycle inspection failed.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
