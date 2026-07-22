using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class SystemManagementWindowsUsbSerialDeviceProviderTests
{
    [Fact]
    public void Constructor_NullQuery_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new SystemManagementWindowsUsbSerialDeviceProvider(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task EnumerateAsync_ValidSnapshot_ShouldExposeRecord()
    {
        // Arrange
        var provider =
            new SystemManagementWindowsUsbSerialDeviceProvider(
                new StubWindowsPnpEntityQuery(
                    new WindowsPnpEntitySnapshot(
                        "USB-SERIAL CH340 (COM10)",
                        "wch.cn",
                        @"USB\VID_1A86&PID_7523\ABC123",
                        "USB-SERIAL CH340")));

        // Act
        IReadOnlyList<WindowsUsbSerialDeviceRecord> records =
            await EnumerateAsync(
                provider);

        // Assert
        WindowsUsbSerialDeviceRecord record =
            Assert.Single(
                records);

        Assert.Equal(
            "COM10",
            record.PortName);

        Assert.Equal(
            (ushort)0x1A86,
            record.VendorId);

        Assert.Equal(
            (ushort)0x7523,
            record.ProductId);

        Assert.Equal(
            "USB-SERIAL CH340",
            record.ProductName);

        Assert.Equal(
            "wch.cn",
            record.ManufacturerName);

        Assert.Equal(
            "ABC123",
            record.SerialNumber);
    }

    [Fact]
    public async Task EnumerateAsync_MalformedSnapshot_ShouldContinue()
    {
        // Arrange
        var provider =
            new SystemManagementWindowsUsbSerialDeviceProvider(
                new StubWindowsPnpEntityQuery(
                    new WindowsPnpEntitySnapshot(
                        "Not a serial device",
                        null,
                        null,
                        null),
                    new WindowsPnpEntitySnapshot(
                        "Arduino Uno (COM4)",
                        "Arduino LLC",
                        @"USB\VID_2341&PID_0043\ABC123",
                        "Arduino Uno")));

        // Act
        IReadOnlyList<WindowsUsbSerialDeviceRecord> records =
            await EnumerateAsync(
                provider);

        // Assert
        WindowsUsbSerialDeviceRecord record =
            Assert.Single(
                records);

        Assert.Equal(
            "COM4",
            record.PortName);
    }

    [Fact]
    public async Task EnumerateAsync_EmptyQuery_ShouldExposeNoRecords()
    {
        // Arrange
        var provider =
            new SystemManagementWindowsUsbSerialDeviceProvider(
                new StubWindowsPnpEntityQuery());

        // Act
        IReadOnlyList<WindowsUsbSerialDeviceRecord> records =
            await EnumerateAsync(
                provider);

        // Assert
        Assert.Empty(
            records);
    }

    [Fact]
    public async Task EnumerateAsync_CancelledBeforeQuery_ShouldNotQuery()
    {
        // Arrange
        var query =
            new CountingWindowsPnpEntityQuery();

        var provider =
            new SystemManagementWindowsUsbSerialDeviceProvider(
                query);

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

        Assert.Equal(
            0,
            query.QueryCount);
    }

    [Fact]
    public async Task EnumerateAsync_QueryFailure_ShouldPropagate()
    {
        // Arrange
        var expectedException =
            new InvalidOperationException(
                "Query failed.");

        var provider =
            new SystemManagementWindowsUsbSerialDeviceProvider(
                new ThrowingWindowsPnpEntityQuery(
                    expectedException));

        // Act
        async Task Act()
        {
            await foreach (
                WindowsUsbSerialDeviceRecord record
                in provider.EnumerateAsync())
            {
            }
        }

        // Assert
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        Assert.Same(
            expectedException,
            actualException);
    }

    [Fact]
    public void QueryText_ShouldSelectRequiredPnpEntityProperties()
    {
        // Assert
        Assert.Contains(
            "Name",
            SystemManagementWindowsPnpEntityQuery.QueryText,
            StringComparison.Ordinal);

        Assert.Contains(
            "Manufacturer",
            SystemManagementWindowsPnpEntityQuery.QueryText,
            StringComparison.Ordinal);

        Assert.Contains(
            "PNPDeviceID",
            SystemManagementWindowsPnpEntityQuery.QueryText,
            StringComparison.Ordinal);

        Assert.Contains(
            "Description",
            SystemManagementWindowsPnpEntityQuery.QueryText,
            StringComparison.Ordinal);

        Assert.Contains(
            "Win32_PnPEntity",
            SystemManagementWindowsPnpEntityQuery.QueryText,
            StringComparison.Ordinal);
    }

    private static async Task<
        IReadOnlyList<WindowsUsbSerialDeviceRecord>> EnumerateAsync(
            IWindowsUsbSerialDeviceProvider provider)
    {
        var records =
            new List<WindowsUsbSerialDeviceRecord>();

        await foreach (
            WindowsUsbSerialDeviceRecord record
            in provider.EnumerateAsync())
        {
            records.Add(
                record);
        }

        return records;
    }

    private sealed class StubWindowsPnpEntityQuery
        : IWindowsPnpEntityQuery
    {
        private readonly IReadOnlyList<
            WindowsPnpEntitySnapshot> _snapshots;

        public StubWindowsPnpEntityQuery(
            params WindowsPnpEntitySnapshot[] snapshots)
        {
            _snapshots =
                snapshots;
        }

        public IReadOnlyList<WindowsPnpEntitySnapshot> Query()
        {
            return _snapshots;
        }
    }

    private sealed class CountingWindowsPnpEntityQuery
        : IWindowsPnpEntityQuery
    {
        public int QueryCount
        {
            get;
            private set;
        }

        public IReadOnlyList<WindowsPnpEntitySnapshot> Query()
        {
            QueryCount++;

            return Array.Empty<WindowsPnpEntitySnapshot>();
        }
    }

    private sealed class ThrowingWindowsPnpEntityQuery
        : IWindowsPnpEntityQuery
    {
        private readonly Exception _exception;

        public ThrowingWindowsPnpEntityQuery(
            Exception exception)
        {
            _exception =
                exception;
        }

        public IReadOnlyList<WindowsPnpEntitySnapshot> Query()
        {
            throw _exception;
        }
    }
}