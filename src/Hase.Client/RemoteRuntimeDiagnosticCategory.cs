namespace Hase.Client;

/// <summary>Identifies the stable area that produced a remote diagnostic.</summary>
public enum RemoteRuntimeDiagnosticCategory
{
    RuntimeAttachment = 0,
    RuntimeConnection = 1,
    RuntimeSynchronization = 2,
    RuntimeRecovery = 3,
    RuntimeProperty = 4,
    RuntimeCommand = 5,
    RuntimeEvent = 6,
    ProtocolExchange = 7,
    TransportBytes = 8
}
