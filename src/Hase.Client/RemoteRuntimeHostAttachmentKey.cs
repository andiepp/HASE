namespace Hase.Client;

/// <summary>
/// Identifies exactly one endpoint attachment within one authoritative
/// runtime host.
/// </summary>
public sealed record RemoteRuntimeHostAttachmentKey
{
    public RemoteRuntimeHostAttachmentKey(
        RemoteRuntimeHostId runtimeHostId,
        RemoteEndpointAttachmentKey attachment)
    {
        RuntimeHostId =
            runtimeHostId
            ?? throw new ArgumentNullException(nameof(runtimeHostId));
        Attachment =
            attachment
            ?? throw new ArgumentNullException(nameof(attachment));
    }

    public RemoteRuntimeHostId RuntimeHostId
    {
        get;
    }

    public RemoteEndpointAttachmentKey Attachment
    {
        get;
    }

    public override string ToString() =>
        $"{RuntimeHostId}/{Attachment}";
}
