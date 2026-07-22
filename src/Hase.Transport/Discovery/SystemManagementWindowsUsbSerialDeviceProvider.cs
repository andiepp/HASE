using System.Runtime.CompilerServices;

namespace Hase.Transport.Discovery;

/// <summary>
/// Supplies raw Windows USB serial-device records from selected
/// Win32_PnPEntity values.
/// </summary>
internal sealed class SystemManagementWindowsUsbSerialDeviceProvider
    : IWindowsUsbSerialDeviceProvider
{
    private readonly IWindowsPnpEntityQuery _query;

    public SystemManagementWindowsUsbSerialDeviceProvider()
        : this(
            new SystemManagementWindowsPnpEntityQuery())
    {
    }

    internal SystemManagementWindowsUsbSerialDeviceProvider(
        IWindowsPnpEntityQuery query)
    {
        ArgumentNullException.ThrowIfNull(
            query);

        _query =
            query;
    }

    public async IAsyncEnumerable<
        WindowsUsbSerialDeviceRecord> EnumerateAsync(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        await Task.Yield();

        cancellationToken
            .ThrowIfCancellationRequested();

        IReadOnlyList<WindowsPnpEntitySnapshot> snapshots =
            _query.Query();

        cancellationToken
            .ThrowIfCancellationRequested();

        foreach (
            WindowsPnpEntitySnapshot snapshot
            in snapshots)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            WindowsUsbSerialDeviceRecord? record =
                WindowsUsbSerialDeviceRecordParser.Parse(
                    snapshot.Name,
                    snapshot.Manufacturer,
                    snapshot.PnpDeviceId,
                    snapshot.Description);

            if (record is not null)
            {
                yield return record;
            }
        }
    }
}