namespace Hase.Transport.Discovery;

/// <summary>
/// Queries the selected Windows Plug-and-Play entity properties needed
/// for USB serial candidate discovery.
/// </summary>
internal interface IWindowsPnpEntityQuery
{
    IReadOnlyList<WindowsPnpEntitySnapshot> Query();
}

/// <summary>
/// Contains an immutable snapshot of selected Win32_PnPEntity values.
/// </summary>
internal sealed class WindowsPnpEntitySnapshot
{
    public WindowsPnpEntitySnapshot(
        string? name,
        string? manufacturer,
        string? pnpDeviceId,
        string? description)
    {
        Name =
            name;

        Manufacturer =
            manufacturer;

        PnpDeviceId =
            pnpDeviceId;

        Description =
            description;
    }

    public string? Name
    {
        get;
    }

    public string? Manufacturer
    {
        get;
    }

    public string? PnpDeviceId
    {
        get;
    }

    public string? Description
    {
        get;
    }
}