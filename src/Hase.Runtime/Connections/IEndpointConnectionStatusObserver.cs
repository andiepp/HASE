namespace Hase.Runtime.Connections;

/// <summary>
/// Receives connection-status changes from a runtime endpoint.
/// </summary>
public interface IEndpointConnectionStatusObserver
{
    void OnEndpointConnectionStatusChanged(
        EndpointConnectionStatusChanged change);
}