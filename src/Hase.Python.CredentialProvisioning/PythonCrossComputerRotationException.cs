namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCrossComputerRotationException : Exception
{
    public PythonCrossComputerRotationException(string code) : base(code) =>
        Code = code;
    public string Code { get; }
}
