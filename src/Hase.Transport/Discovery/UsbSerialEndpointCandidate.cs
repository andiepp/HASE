namespace Hase.Transport.Discovery;

/// <summary>
/// Represents one operating-system serial connection target that may
/// expose a HASE compact endpoint.
/// </summary>
/// <remarks>
/// USB metadata is descriptive candidate information only.
/// It is not authoritative HASE endpoint identity.
///
/// Candidate identity is defined by the port name or operating-system
/// device path.
/// </remarks>
public sealed class UsbSerialEndpointCandidate
    : IEquatable<UsbSerialEndpointCandidate>
{
    /// <summary>
    /// Initializes a new USB serial endpoint candidate.
    /// </summary>
    /// <param name="portName">
    /// The operating-system serial port name or device path.
    /// </param>
    /// <param name="vendorId">
    /// The optional USB vendor identifier.
    /// </param>
    /// <param name="productId">
    /// The optional USB product identifier.
    /// </param>
    /// <param name="productName">
    /// The optional USB product name.
    /// </param>
    /// <param name="manufacturerName">
    /// The optional USB manufacturer name.
    /// </param>
    /// <param name="serialNumber">
    /// The optional USB serial number.
    /// </param>
    public UsbSerialEndpointCandidate(
        string portName,
        ushort? vendorId = null,
        ushort? productId = null,
        string? productName = null,
        string? manufacturerName = null,
        string? serialNumber = null)
    {
        if (string.IsNullOrWhiteSpace(
            portName))
        {
            throw new ArgumentException(
                "The serial port name must not be empty.",
                nameof(portName));
        }

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

    /// <summary>
    /// Gets the operating-system serial port name or device path.
    /// </summary>
    public string PortName
    {
        get;
    }

    /// <summary>
    /// Gets the optional USB vendor identifier.
    /// </summary>
    public ushort? VendorId
    {
        get;
    }

    /// <summary>
    /// Gets the optional USB product identifier.
    /// </summary>
    public ushort? ProductId
    {
        get;
    }

    /// <summary>
    /// Gets the optional USB product name.
    /// </summary>
    public string? ProductName
    {
        get;
    }

    /// <summary>
    /// Gets the optional USB manufacturer name.
    /// </summary>
    public string? ManufacturerName
    {
        get;
    }

    /// <summary>
    /// Gets the optional USB serial number.
    /// </summary>
    public string? SerialNumber
    {
        get;
    }

    /// <inheritdoc />
    public bool Equals(
        UsbSerialEndpointCandidate? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(
                PortName,
                other.PortName);
    }

    /// <inheritdoc />
    public override bool Equals(
        object? obj)
    {
        return Equals(
            obj as UsbSerialEndpointCandidate);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(
            PortName);
    }
}