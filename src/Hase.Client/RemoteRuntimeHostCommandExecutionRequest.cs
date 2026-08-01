namespace Hase.Client;

public sealed record RemoteRuntimeHostCommandExecutionRequest
{
    public RemoteRuntimeHostCommandExecutionRequest(RemoteRuntimeHostId runtimeHostId, RemoteCommandExecutionRequest request)
    {
        RuntimeHostId = runtimeHostId ?? throw new ArgumentNullException(nameof(runtimeHostId));
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }
    public RemoteRuntimeHostId RuntimeHostId { get; }
    public RemoteCommandExecutionRequest Request { get; }
}
