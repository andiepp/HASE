namespace Hase.Client;

/// <summary>Publishes normalized remote Runtime Host diagnostic observations.</summary>
public interface IRuntimeHostDiagnosticSource
{
    event EventHandler<RemoteRuntimeDiagnosticObservedEventArgs>?
        DiagnosticObserved;

    event EventHandler<RemoteRuntimeDiagnosticStreamFaultedEventArgs>?
        DiagnosticStreamFaulted;
}
