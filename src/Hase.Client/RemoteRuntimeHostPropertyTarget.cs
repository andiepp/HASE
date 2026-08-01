namespace Hase.Client;

public sealed record RemoteRuntimeHostPropertyTarget
{
    public RemoteRuntimeHostPropertyTarget(RemoteRuntimeHostId runtimeHostId, RemotePropertyTarget target)
    {
        RuntimeHostId = runtimeHostId ?? throw new ArgumentNullException(nameof(runtimeHostId));
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }
    public RemoteRuntimeHostId RuntimeHostId { get; }
    public RemotePropertyTarget Target { get; }
}
