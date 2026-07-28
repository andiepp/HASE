namespace Hase.DesktopHost;

public sealed record DesktopRuntimeEndpointSnapshot(
    string EndpointId,
    string DisplayName,
    string ConnectionState,
    string AttachmentGeneration)
{
    public string? Description
    {
        get;
        init;
    }

    public IReadOnlyList<DesktopRuntimeInstrumentSnapshot> Instruments
    {
        get;
        init;
    } =
        [];
}
