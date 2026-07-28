namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimeInstrumentViewModel
{
    public DesktopRuntimeInstrumentViewModel(
        DesktopRuntimeInstrumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        InstrumentId =
            snapshot.InstrumentId;
        Name =
            snapshot.Name;
        Kind =
            snapshot.Kind;
        Manufacturer =
            snapshot.Manufacturer
            ?? string.Empty;
        Model =
            snapshot.Model
            ?? string.Empty;
        SerialNumber =
            snapshot.SerialNumber
            ?? string.Empty;
        FirmwareVersion =
            snapshot.FirmwareVersion
            ?? string.Empty;
        HardwareRevision =
            snapshot.HardwareRevision
            ?? string.Empty;
        Description =
            snapshot.Description
            ?? string.Empty;
        Properties =
            snapshot.Properties
                .Select(
                    property =>
                        new DesktopRuntimePropertyViewModel(
                            property))
                .ToArray();
    }

    public string InstrumentId
    {
        get;
    }

    public string Name
    {
        get;
    }

    public string Kind
    {
        get;
    }

    public string Manufacturer
    {
        get;
    }

    public string Model
    {
        get;
    }

    public string SerialNumber
    {
        get;
    }

    public string FirmwareVersion
    {
        get;
    }

    public string HardwareRevision
    {
        get;
    }

    public string Description
    {
        get;
    }

    public IReadOnlyList<DesktopRuntimePropertyViewModel> Properties
    {
        get;
    }

    public int PropertyCount =>
        Properties.Count;
}
