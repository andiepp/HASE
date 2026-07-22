using System.Runtime.CompilerServices;

namespace Hase.Transport.Discovery;

/// <summary>
/// Enumerates platform-neutral USB serial candidates from Windows
/// serial-device records.
/// </summary>
/// <remarks>
/// Windows metadata identifies connection candidates only.
/// Every candidate requires separate Compact Serial Protocol bootstrap
/// verification before it can be accepted as a HASE endpoint.
/// </remarks>
public sealed class WindowsUsbSerialEndpointCandidateSource
    : IUsbSerialEndpointCandidateSource
{
    private readonly IWindowsUsbSerialDeviceProvider _deviceProvider;

    internal WindowsUsbSerialEndpointCandidateSource(
        IWindowsUsbSerialDeviceProvider deviceProvider)
    {
        ArgumentNullException.ThrowIfNull(
            deviceProvider);

        _deviceProvider =
            deviceProvider;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<
        UsbSerialEndpointCandidate> EnumerateAsync(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
    {
        var observedCandidates =
            new HashSet<UsbSerialEndpointCandidate>();

        await foreach (
            WindowsUsbSerialDeviceRecord record
            in _deviceProvider
                .EnumerateAsync(
                    cancellationToken)
                .WithCancellation(
                    cancellationToken))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            UsbSerialEndpointCandidate? candidate =
                CreateCandidate(
                    record);

            if (candidate is not null
                && observedCandidates.Add(
                    candidate))
            {
                yield return candidate;
            }
        }
    }

    internal static UsbSerialEndpointCandidate? CreateCandidate(
        WindowsUsbSerialDeviceRecord? record)
    {
        if (record is null
            || string.IsNullOrWhiteSpace(
                record.PortName))
        {
            return null;
        }

        string normalizedPortName =
            record.PortName
                .Trim()
                .ToUpperInvariant();

        return new UsbSerialEndpointCandidate(
            normalizedPortName,
            record.VendorId,
            record.ProductId,
            NormalizeOptionalMetadata(
                record.ProductName),
            NormalizeOptionalMetadata(
                record.ManufacturerName),
            NormalizeOptionalMetadata(
                record.SerialNumber));
    }

    private static string? NormalizeOptionalMetadata(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
            value))
        {
            return null;
        }

        return value.Trim();
    }
}