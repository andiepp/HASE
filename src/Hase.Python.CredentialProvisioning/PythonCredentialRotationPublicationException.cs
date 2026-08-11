namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialRotationPublicationException : Exception
{
    public PythonCredentialRotationPublicationException(string code)
        : base("Python credential rotation publication failed.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
