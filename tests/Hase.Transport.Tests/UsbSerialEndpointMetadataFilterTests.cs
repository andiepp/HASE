using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class UsbSerialEndpointMetadataFilterTests
{
    private static readonly UsbSerialEndpointCandidate CompleteCandidate =
        new(
            "COM10",
            vendorId: 0x1A86,
            productId: 0x7523,
            productName: "USB Serial",
            manufacturerName: "QinHeng Electronics",
            serialNumber: "ABC123");

    [Fact]
    public void Constructor_CompleteCriteria_ShouldExposeValues()
    {
        // Act
        var filter =
            new UsbSerialEndpointMetadataFilter(
                portName: "COM10",
                vendorId: 0x1A86,
                productId: 0x7523,
                productName: "USB Serial",
                manufacturerName: "QinHeng Electronics",
                serialNumber: "ABC123");

        // Assert
        Assert.Equal(
            "COM10",
            filter.PortName);

        Assert.Equal(
            (ushort)0x1A86,
            filter.VendorId);

        Assert.Equal(
            (ushort)0x7523,
            filter.ProductId);

        Assert.Equal(
            "USB Serial",
            filter.ProductName);

        Assert.Equal(
            "QinHeng Electronics",
            filter.ManufacturerName);

        Assert.Equal(
            "ABC123",
            filter.SerialNumber);
    }

    [Fact]
    public void IsMatch_EmptyFilter_ShouldMatchCandidate()
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter();

        // Act
        bool result =
            filter.IsMatch(
                CompleteCandidate);

        // Assert
        Assert.True(
            result);
    }

    [Fact]
    public void IsMatch_AllCriteriaMatch_ShouldReturnTrue()
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter(
                portName: "COM10",
                vendorId: 0x1A86,
                productId: 0x7523,
                productName: "usb serial",
                manufacturerName: "qinheng electronics",
                serialNumber: "ABC123");

        // Act
        bool result =
            filter.IsMatch(
                CompleteCandidate);

        // Assert
        Assert.True(
            result);
    }

    [Theory]
    [InlineData("COM11")]
    [InlineData("com10")]
    public void IsMatch_PortNameDoesNotMatch_ShouldReturnFalse(
        string portName)
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter(
                portName: portName);

        // Act
        bool result =
            filter.IsMatch(
                CompleteCandidate);

        // Assert
        Assert.False(
            result);
    }

    [Theory]
    [InlineData(0x2341)]
    [InlineData(0x0403)]
    public void IsMatch_VendorIdDoesNotMatch_ShouldReturnFalse(
        ushort vendorId)
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter(
                vendorId: vendorId);

        // Act
        bool result =
            filter.IsMatch(
                CompleteCandidate);

        // Assert
        Assert.False(
            result);
    }

    [Theory]
    [InlineData(0x0043)]
    [InlineData(0x6001)]
    public void IsMatch_ProductIdDoesNotMatch_ShouldReturnFalse(
        ushort productId)
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter(
                productId: productId);

        // Act
        bool result =
            filter.IsMatch(
                CompleteCandidate);

        // Assert
        Assert.False(
            result);
    }

    [Fact]
    public void IsMatch_ProductNameComparison_ShouldIgnoreCase()
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter(
                productName: "usb SERIAL");

        // Act
        bool result =
            filter.IsMatch(
                CompleteCandidate);

        // Assert
        Assert.True(
            result);
    }

    [Fact]
    public void IsMatch_ManufacturerNameComparison_ShouldIgnoreCase()
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter(
                manufacturerName: "QINHENG electronics");

        // Act
        bool result =
            filter.IsMatch(
                CompleteCandidate);

        // Assert
        Assert.True(
            result);
    }

    [Fact]
    public void IsMatch_SerialNumberComparison_ShouldBeCaseSensitive()
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter(
                serialNumber: "abc123");

        // Act
        bool result =
            filter.IsMatch(
                CompleteCandidate);

        // Assert
        Assert.False(
            result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_InvalidPortNameCriterion_ShouldThrow(
        string portName)
    {
        // Act
        void Act()
        {
            _ = new UsbSerialEndpointMetadataFilter(
                portName: portName);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_InvalidProductNameCriterion_ShouldThrow(
        string productName)
    {
        // Act
        void Act()
        {
            _ = new UsbSerialEndpointMetadataFilter(
                productName: productName);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_InvalidManufacturerNameCriterion_ShouldThrow(
        string manufacturerName)
    {
        // Act
        void Act()
        {
            _ = new UsbSerialEndpointMetadataFilter(
                manufacturerName: manufacturerName);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_InvalidSerialNumberCriterion_ShouldThrow(
        string serialNumber)
    {
        // Act
        void Act()
        {
            _ = new UsbSerialEndpointMetadataFilter(
                serialNumber: serialNumber);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void IsMatch_RequiredMetadataMissing_ShouldReturnFalse()
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter(
                vendorId: 0x1A86);

        var candidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        // Act
        bool result =
            filter.IsMatch(
                candidate);

        // Assert
        Assert.False(
            result);
    }

    [Fact]
    public void IsMatch_NullCandidate_ShouldThrow()
    {
        // Arrange
        var filter =
            new UsbSerialEndpointMetadataFilter();

        // Act
        void Act()
        {
            _ = filter.IsMatch(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }
}
