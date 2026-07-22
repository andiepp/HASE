using System.Runtime.CompilerServices;
using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class WindowsUsbSerialDeviceProviderContractTests
{
    [Fact]
    public async Task EnumerateAsync_ShouldExposeRawRecords()
    {
        // Arrange
        var expectedRecord =
            new WindowsUsbSerialDeviceRecord(
                "COM10",
                vendorId: 0x1A86,
                productId: 0x7523,
                productName: "USB Serial",
                manufacturerName: "QinHeng Electronics",
                serialNumber: "ABC123");

        IWindowsUsbSerialDeviceProvider provider =
            new StubWindowsUsbSerialDeviceProvider(
                expectedRecord);

        // Act
        var records =
            new List<WindowsUsbSerialDeviceRecord>();

        await foreach (
            WindowsUsbSerialDeviceRecord record
            in provider.EnumerateAsync())
        {
            records.Add(
                record);
        }

        // Assert
        WindowsUsbSerialDeviceRecord actualRecord =
            Assert.Single(
                records);

        Assert.Same(
            expectedRecord,
            actualRecord);
    }

    [Fact]
    public async Task EnumerateAsync_CancelledToken_ShouldStopEnumeration()
    {
        // Arrange
        IWindowsUsbSerialDeviceProvider provider =
            new StubWindowsUsbSerialDeviceProvider();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        async Task Act()
        {
            await foreach (
                WindowsUsbSerialDeviceRecord record
                in provider.EnumerateAsync(
                    cancellationTokenSource.Token))
            {
            }
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);
    }

    private sealed class StubWindowsUsbSerialDeviceProvider
        : IWindowsUsbSerialDeviceProvider
    {
        private readonly IReadOnlyList<
            WindowsUsbSerialDeviceRecord> _records;

        public StubWindowsUsbSerialDeviceProvider(
            params WindowsUsbSerialDeviceRecord[] records)
        {
            _records =
                records;
        }

        public async IAsyncEnumerable<
            WindowsUsbSerialDeviceRecord> EnumerateAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            cancellationToken
                .ThrowIfCancellationRequested();

            foreach (
                WindowsUsbSerialDeviceRecord record
                in _records)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                yield return record;
            }
        }
    }
}