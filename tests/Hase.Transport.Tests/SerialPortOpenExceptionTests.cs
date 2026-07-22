using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class SerialPortOpenExceptionTests
{
    [Theory]
    [InlineData(SerialPortOpenFailure.Busy)]
    [InlineData(SerialPortOpenFailure.Unavailable)]
    [InlineData(SerialPortOpenFailure.AccessDenied)]
    [InlineData(SerialPortOpenFailure.Failed)]
    public void Constructor_ValidValues_ShouldExposeValues(
        SerialPortOpenFailure failure)
    {
        // Arrange
        var innerException =
            new IOException(
                "Open failed.");

        // Act
        var exception =
            new SerialPortOpenException(
                "COM10",
                failure,
                innerException);

        // Assert
        Assert.Equal(
            "COM10",
            exception.PortName);

        Assert.Equal(
            failure,
            exception.Failure);

        Assert.Same(
            innerException,
            exception.InnerException);

        Assert.Contains(
            "COM10",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            failure.ToString(),
            exception.Message,
            StringComparison.Ordinal);
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
            _ = new SerialPortOpenException(
                portName,
                SerialPortOpenFailure.Failed,
                new IOException());
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_InvalidFailure_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new SerialPortOpenException(
                "COM10",
                (SerialPortOpenFailure)999,
                new IOException());
        }

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Constructor_NullInnerException_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new SerialPortOpenException(
                "COM10",
                SerialPortOpenFailure.Failed,
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }
}