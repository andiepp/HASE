namespace Hase.Transport.Discovery;

/// <summary>
/// Filters USB serial candidates by optional connection and USB metadata.
/// </summary>
/// <remarks>
/// Every configured criterion must match.
///
/// Filtering reduces the candidates selected for active verification.
/// It does not establish HASE endpoint identity, select a descriptor,
/// or verify Compact Serial Protocol compatibility.
/// </remarks>
public sealed class UsbSerialEndpointMetadataFilter
    : IUsbSerialEndpointCandidateFilter
{
    /// <summary>
    /// Initializes a new USB serial endpoint metadata filter.
    /// </summary>
    /// <param name="portName">
    /// The optional required port name or operating-system device path.
    /// </param>
    /// <param name="vendorId">
    /// The optional required USB vendor identifier.
    /// </param>
    /// <param name="productId">
    /// The optional required USB product identifier.
    /// </param>
    /// <param name="productName">
    /// The optional required USB product name.
    /// </param>
    /// <param name="manufacturerName">
    /// The optional required USB manufacturer name.
    /// </param>
    /// <param name="serialNumber">
    /// The optional required USB serial number.
    /// </param>
    public UsbSerialEndpointMetadataFilter(
        string? portName = null,
        ushort? vendorId = null,
        ushort? productId = null,
        string? productName = null,
        string? manufacturerName = null,
        string? serialNumber = null)
    {
        PortName =
            ValidateOptionalText(
                portName,
                nameof(portName));

        VendorId =
            vendorId;

        ProductId =
            productId;

        ProductName =
            ValidateOptionalText(
                productName,
                nameof(productName));

        ManufacturerName =
            ValidateOptionalText(
                manufacturerName,
                nameof(manufacturerName));

        SerialNumber =
            ValidateOptionalText(
                serialNumber,
                nameof(serialNumber));
    }

    /// <summary>
    /// Gets the optional required port name or device path.
    /// </summary>
    public string? PortName
    {
        get;
    }

    /// <summary>
    /// Gets the optional required USB vendor identifier.
    /// </summary>
    public ushort? VendorId
    {
        get;
    }

    /// <summary>
    /// Gets the optional required USB product identifier.
    /// </summary>
    public ushort? ProductId
    {
        get;
    }

    /// <summary>
    /// Gets the optional required USB product name.
    /// </summary>
    public string? ProductName
    {
        get;
    }

    /// <summary>
    /// Gets the optional required USB manufacturer name.
    /// </summary>
    public string? ManufacturerName
    {
        get;
    }

    /// <summary>
    /// Gets the optional required USB serial number.
    /// </summary>
    public string? SerialNumber
    {
        get;
    }

    /// <inheritdoc />
    public bool IsMatch(
        UsbSerialEndpointCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(
            candidate);

        return MatchesOrdinal(
                PortName,
                candidate.PortName)
            && MatchesValue(
                VendorId,
                candidate.VendorId)
            && MatchesValue(
                ProductId,
                candidate.ProductId)
            && MatchesOrdinalIgnoreCase(
                ProductName,
                candidate.ProductName)
            && MatchesOrdinalIgnoreCase(
                ManufacturerName,
                candidate.ManufacturerName)
            && MatchesOrdinal(
                SerialNumber,
                candidate.SerialNumber);
    }

    private static string? ValidateOptionalText(
        string? value,
        string parameterName)
    {
        if (value is not null
            && string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "A configured metadata criterion must not be empty.",
                parameterName);
        }

        return value;
    }

    private static bool MatchesValue<T>(
        T? expected,
        T? actual)
        where T : struct, IEquatable<T>
    {
        return !expected.HasValue
            || actual.HasValue
            && expected.Value.Equals(
                actual.Value);
    }

    private static bool MatchesOrdinal(
        string? expected,
        string? actual)
    {
        return expected is null
            || actual is not null
            && StringComparer.Ordinal.Equals(
                expected,
                actual);
    }

    private static bool MatchesOrdinalIgnoreCase(
        string? expected,
        string? actual)
    {
        return expected is null
            || actual is not null
            && StringComparer.OrdinalIgnoreCase.Equals(
                expected,
                actual);
    }
}