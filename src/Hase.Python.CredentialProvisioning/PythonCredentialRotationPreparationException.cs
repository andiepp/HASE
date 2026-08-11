namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialRotationPreparationException : Exception
{
    public PythonCredentialRotationPreparationException(string code)
        : base("Python credential rotation preparation failed.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
