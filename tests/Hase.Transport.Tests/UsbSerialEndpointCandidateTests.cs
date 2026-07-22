using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class UsbSerialEndpointCandidateTests
{
    [Fact]
    public void Constructor_CompleteMetadata_ShouldExposeValues()
    {
        // Arrange
        const string portName =
            "COM10";

        const ushort vendorId =
            0x1A86;

        const ushort productId =
            0x7523;

        const string productName =
            "USB Serial";

        const string manufacturerName =
            "QinHeng Electronics";

        const string serialNumber =
            "ABC123";

        // Act
        var candidate =
            new UsbSerialEndpointCandidate(
                portName,
                vendorId,
                productId,
                productName,
                manufacturerName,
                serialNumber);

        // Assert
        Assert.Equal(
            portName,
            candidate.PortName);

        Assert.Equal(
            vendorId,
            candidate.VendorId);

        Assert.Equal(
            productId,
            candidate.ProductId);

        Assert.Equal(
            productName,
            candidate.ProductName);

        Assert.Equal(
            manufacturerName,
            candidate.ManufacturerName);

        Assert.Equal(
            serialNumber,
            candidate.SerialNumber);
    }

    [Fact]
    public void Constructor_PortNameOnly_ShouldExposeNullMetadata()
    {
        // Act
        var candidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        // Assert
        Assert.Equal(
            "COM10",
            candidate.PortName);

        Assert.Null(
            candidate.VendorId);

        Assert.Null(
            candidate.ProductId);

        Assert.Null(
            candidate.ProductName);

        Assert.Null(
            candidate.ManufacturerName);

        Assert.Null(
            candidate.SerialNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_InvalidPortName_ShouldThrow(
        string portName)
    {
        // Act
        void Act()
        {
            _ = new UsbSerialEndpointCandidate(
                portName);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Equals_SamePortName_ShouldBeEqual()
    {
        // Arrange
        var first =
            new UsbSerialEndpointCandidate(
                "COM10",
                vendorId: 0x1A86,
                productId: 0x7523,
                productName: "USB Serial",
                manufacturerName: "QinHeng Electronics",
                serialNumber: "ABC123");

        var second =
            new UsbSerialEndpointCandidate(
                "COM10",
                vendorId: 0x2341,
                productId: 0x0043,
                productName: "Arduino Uno",
                manufacturerName: "Arduino",
                serialNumber: "XYZ789");

        // Act
        bool equal =
            first.Equals(
                second);

        // Assert
        Assert.True(
            equal);

        Assert.Equal(
            first.GetHashCode(),
            second.GetHashCode());
    }

    [Theory]
    [InlineData("COM11")]
    [InlineData("com10")]
    [InlineData("/dev/ttyUSB0")]
    public void Equals_DifferentPortName_ShouldNotBeEqual(
        string portName)
    {
        // Arrange
        var first =
            new UsbSerialEndpointCandidate(
                "COM10");

        var second =
            new UsbSerialEndpointCandidate(
                portName);

        // Act
        bool equal =
            first.Equals(
                second);

        // Assert
        Assert.False(
            equal);
    }

    [Fact]
    public void Equals_Null_ShouldNotBeEqual()
    {
        // Arrange
        var candidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        // Act
        bool equal =
            candidate.Equals(
                null);

        // Assert
        Assert.False(
            equal);
    }

    [Fact]
    public void Equals_ObjectWithDifferentType_ShouldNotBeEqual()
    {
        // Arrange
        var candidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        // Act
        bool equal =
            candidate.Equals(
                "COM10");

        // Assert
        Assert.False(
            equal);
    }
}