namespace Hase.Client;

public enum RemoteRuntimeDiagnosticStreamFailureKind
{
    AuthorizationDenied = 0,
    AuthenticationFailed = 1,
    TransportUnavailable = 2,
    Gap = 3,
    InvalidRemoteContract = 4,
    Unknown = 5
}
