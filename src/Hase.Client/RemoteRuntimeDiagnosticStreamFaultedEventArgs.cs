namespace Hase.Client;

public sealed class RemoteRuntimeDiagnosticStreamFaultedEventArgs : EventArgs
{
    public RemoteRuntimeDiagnosticStreamFaultedEventArgs(
        RemoteRuntimeDiagnosticStreamFailureKind kind,
        Exception exception)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        Kind = kind;
        Exception = exception
            ?? throw new ArgumentNullException(nameof(exception));
    }

    public RemoteRuntimeDiagnosticStreamFailureKind Kind { get; }
    public Exception Exception { get; }
}
