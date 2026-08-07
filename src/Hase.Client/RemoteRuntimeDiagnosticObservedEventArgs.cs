namespace Hase.Client;

public sealed class RemoteRuntimeDiagnosticObservedEventArgs : EventArgs
{
    public RemoteRuntimeDiagnosticObservedEventArgs(
        RemoteRuntimeDiagnosticObservation observation)
    {
        Observation = observation
            ?? throw new ArgumentNullException(nameof(observation));
    }

    public RemoteRuntimeDiagnosticObservation Observation { get; }
}
