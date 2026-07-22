using System.Runtime.CompilerServices;
using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class WindowsUsbSerialEndpointCandidateSourceTests
{
    [Fact]
    public void Constructor_NullProvider_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new WindowsUsbSerialEndpointCandidateSource(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task EnumerateAsync_CompleteRecord_ShouldMapCandidate()
    {
        // Arrange
        var record =
            new WindowsUsbSerialDeviceRecord(
                "COM10",
                vendorId: 0x1A86,
                productId: 0x7523,
                productName: "USB Serial",
                manufacturerName: "QinHeng Electronics",
                serialNumber: "ABC123");

        var source =
            new WindowsUsbSerialEndpointCandidateSource(
                new StubWindowsUsbSerialDeviceProvider(
                    record));

        // Act
        IReadOnlyList<UsbSerialEndpointCandidate> candidates =
            await EnumerateAsync(
                source);

        // Assert
        UsbSerialEndpointCandidate candidate =
            Assert.Single(
                candidates);

        Assert.Equal(
            "COM10",
            candidate.PortName);

        Assert.Equal(
            (ushort)0x1A86,
            candidate.VendorId);

        Assert.Equal(
            (ushort)0x7523,
            candidate.ProductId);

        Assert.Equal(
            "USB Serial",
            candidate.ProductName);

        Assert.Equal(
            "QinHeng Electronics",
            candidate.ManufacturerName);

        Assert.Equal(
            "ABC123",
            candidate.SerialNumber);
    }

    [Fact]
    public async Task EnumerateAsync_PortName_ShouldNormalizeWindowsName()
    {
        // Arrange
        var source =
            new WindowsUsbSerialEndpointCandidateSource(
                new StubWindowsUsbSerialDeviceProvider(
                    new WindowsUsbSerialDeviceRecord(
                        " com10 ")));

        // Act
        IReadOnlyList<UsbSerialEndpointCandidate> candidates =
            await EnumerateAsync(
                source);

        // Assert
        UsbSerialEndpointCandidate candidate =
            Assert.Single(
                candidates);

        Assert.Equal(
            "COM10",
            candidate.PortName);
    }

    [Fact]
    public async Task EnumerateAsync_MetadataWhitespace_ShouldNormalizeMetadata()
    {
        // Arrange
        var source =
            new WindowsUsbSerialEndpointCandidateSource(
                new StubWindowsUsbSerialDeviceProvider(
                    new WindowsUsbSerialDeviceRecord(
                        "COM10",
                        productName: " USB Serial ",
                        manufacturerName: " QinHeng Electronics ",
                        serialNumber: " ABC123 ")));

        // Act
        IReadOnlyList<UsbSerialEndpointCandidate> candidates =
            await EnumerateAsync(
                source);

        // Assert
        UsbSerialEndpointCandidate candidate =
            Assert.Single(
                candidates);

        Assert.Equal(
            "USB Serial",
            candidate.ProductName);

        Assert.Equal(
            "QinHeng Electronics",
            candidate.ManufacturerName);

        Assert.Equal(
            "ABC123",
            candidate.SerialNumber);
    }

    [Fact]
    public async Task EnumerateAsync_EmptyOptionalMetadata_ShouldExposeNull()
    {
        // Arrange
        var source =
            new WindowsUsbSerialEndpointCandidateSource(
                new StubWindowsUsbSerialDeviceProvider(
                    new WindowsUsbSerialDeviceRecord(
                        "COM10",
                        productName: "",
                        manufacturerName: " ",
                        serialNumber: "\t")));

        // Act
        IReadOnlyList<UsbSerialEndpointCandidate> candidates =
            await EnumerateAsync(
                source);

        // Assert
        UsbSerialEndpointCandidate candidate =
            Assert.Single(
                candidates);

        Assert.Null(
            candidate.ProductName);

        Assert.Null(
            candidate.ManufacturerName);

        Assert.Null(
            candidate.SerialNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task EnumerateAsync_InvalidPortName_ShouldIgnoreRecord(
        string? portName)
    {
        // Arrange
        var source =
            new WindowsUsbSerialEndpointCandidateSource(
                new StubWindowsUsbSerialDeviceProvider(
                    new WindowsUsbSerialDeviceRecord(
                        portName)));

        // Act
        IReadOnlyList<UsbSerialEndpointCandidate> candidates =
            await EnumerateAsync(
                source);

        // Assert
        Assert.Empty(
            candidates);
    }

    [Fact]
    public async Task EnumerateAsync_DuplicateNormalizedPort_ShouldEmitOnce()
    {
        // Arrange
        var source =
            new WindowsUsbSerialEndpointCandidateSource(
                new StubWindowsUsbSerialDeviceProvider(
                    new WindowsUsbSerialDeviceRecord(
                        "COM10"),
                    new WindowsUsbSerialDeviceRecord(
                        " com10 ")));

        // Act
        IReadOnlyList<UsbSerialEndpointCandidate> candidates =
            await EnumerateAsync(
                source);

        // Assert
        UsbSerialEndpointCandidate candidate =
            Assert.Single(
                candidates);

        Assert.Equal(
            "COM10",
            candidate.PortName);
    }

    [Fact]
    public async Task EnumerateAsync_MalformedRecord_ShouldContinueWithNext()
    {
        // Arrange
        var source =
            new WindowsUsbSerialEndpointCandidateSource(
                new StubWindowsUsbSerialDeviceProvider(
                    new WindowsUsbSerialDeviceRecord(
                        null),
                    new WindowsUsbSerialDeviceRecord(
                        "COM10")));

        // Act
        IReadOnlyList<UsbSerialEndpointCandidate> candidates =
            await EnumerateAsync(
                source);

        // Assert
        UsbSerialEndpointCandidate candidate =
            Assert.Single(
                candidates);

        Assert.Equal(
            "COM10",
            candidate.PortName);
    }

    [Fact]
    public async Task EnumerateAsync_CancelledToken_ShouldPropagateCancellation()
    {
        // Arrange
        var source =
            new WindowsUsbSerialEndpointCandidateSource(
                new StubWindowsUsbSerialDeviceProvider());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        async Task Act()
        {
            await foreach (
                UsbSerialEndpointCandidate candidate
                in source.EnumerateAsync(
                    cancellationTokenSource.Token))
            {
            }
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);
    }

    private static async Task<
        IReadOnlyList<UsbSerialEndpointCandidate>> EnumerateAsync(
            IUsbSerialEndpointCandidateSource source)
    {
        var candidates =
            new List<UsbSerialEndpointCandidate>();

        await foreach (
            UsbSerialEndpointCandidate candidate
            in source.EnumerateAsync())
        {
            candidates.Add(
                candidate);
        }

        return candidates;
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