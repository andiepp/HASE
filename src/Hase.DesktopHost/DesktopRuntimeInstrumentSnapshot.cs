namespace Hase.DesktopHost;

public sealed record DesktopRuntimeInstrumentSnapshot(
    string InstrumentId,
    string Name,
    string Kind,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? FirmwareVersion,
    string? HardwareRevision,
    string? Description)
{
    public IReadOnlyList<DesktopRuntimePropertySnapshot> Properties
    {
        get;
        init;
    } =
        [];

    public IReadOnlyList<DesktopRuntimeCommandSnapshot> Commands
    {
        get;
        init;
    } =
        [];

    public IReadOnlyList<DesktopRuntimeEventSnapshot> Events
    {
        get;
        init;
    } =
        [];
}
