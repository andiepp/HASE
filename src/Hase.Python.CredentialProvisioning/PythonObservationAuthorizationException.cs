namespace Hase.Python.CredentialProvisioning;
public sealed class PythonObservationAuthorizationException : Exception
{
    public PythonObservationAuthorizationException(string code)
        : base($"Python observation authorization failed: {code}.") => Code = code;
    public string Code { get; }
}
