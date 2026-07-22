namespace Hase.Transport.Discovery;

/// <summary>
/// Supplies raw Windows serial-device records to the platform-neutral
/// USB serial candidate source.
/// </summary>
internal interface IWindowsUsbSerialDeviceProvider
{
    IAsyncEnumerable<WindowsUsbSerialDeviceRecord> EnumerateAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents one raw record produced by a Windows serial-device
/// metadata provider.
/// </summary>
/// <remarks>
/// Raw provider records are intentionally not validated. The candidate
/// source isolates malformed records before exposing platform-neutral
/// candidates.
/// </remarks>
internal sealed class WindowsUsbSerialDeviceRecord
{
    public WindowsUsbSerialDeviceRecord(
        string? portName,
        ushort? vendorId = null,
        ushort? productId = null,
        string? productName = null,
        string? manufacturerName = null,
        string? serialNumber = null)
    {
        PortName =
            portName;

        VendorId =
            vendorId;

        ProductId =
            productId;

        ProductName =
            productName;

        ManufacturerName =
            manufacturerName;

        SerialNumber =
            serialNumber;
    }

    public string? PortName
    {
        get;
    }

    public ushort? VendorId
    {
        get;
    }

    public ushort? ProductId
    {
        get;
    }

    public string? ProductName
    {
        get;
    }

    public string? ManufacturerName
    {
        get;
    }

    public string? SerialNumber
    {
        get;
    }
}