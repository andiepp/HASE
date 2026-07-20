using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class SerialTransportOptionsTests
{
    [Fact]
    public void Constructor_PortAndBaudRate_ShouldUseEightNoneOneDefaults()
    {
        var options =
            new SerialTransportOptions(
                "COM5",
                115200);

        Assert.Equal(
            "COM5",
            options.PortName);

        Assert.Equal(
            115200,
            options.BaudRate);

        Assert.Equal(
            8,
            options.DataBits);

        Assert.Equal(
            SerialParity.None,
            options.Parity);

        Assert.Equal(
            SerialStopBits.One,
            options.StopBits);

        Assert.Equal(
            SerialHandshake.None,
            options.Handshake);
    }

    [Fact]
    public void Constructor_AllSettings_ShouldStoreSettings()
    {
        var options =
            new SerialTransportOptions(
                "/dev/ttyUSB0",
                57600,
                7,
                SerialParity.Even,
                SerialStopBits.Two,
                SerialHandshake.XOnXOff);

        Assert.Equal(
            "/dev/ttyUSB0",
            options.PortName);

        Assert.Equal(
            57600,
            options.BaudRate);

        Assert.Equal(
            7,
            options.DataBits);

        Assert.Equal(
            SerialParity.Even,
            options.Parity);

        Assert.Equal(
            SerialStopBits.Two,
            options.StopBits);

        Assert.Equal(
            SerialHandshake.XOnXOff,
            options.Handshake);
    }

    [Fact]
    public void Constructor_NullPortName_ShouldThrow()
    {
        void Act()
        {
            _ = new SerialTransportOptions(
                null!,
                115200);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyOrWhitespacePortName_ShouldThrow(
        string portName)
    {
        void Act()
        {
            _ = new SerialTransportOptions(
                portName,
                115200);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveBaudRate_ShouldThrow(
        int baudRate)
    {
        void Act()
        {
            _ = new SerialTransportOptions(
                "COM5",
                baudRate);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "baudRate",
            exception.ParamName);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void Constructor_InvalidDataBits_ShouldThrow(
        int dataBits)
    {
        void Act()
        {
            _ = new SerialTransportOptions(
                "COM5",
                115200,
                dataBits,
                SerialParity.None,
                SerialStopBits.One,
                SerialHandshake.None);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "dataBits",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_InvalidParity_ShouldThrow()
    {
        void Act()
        {
            _ = new SerialTransportOptions(
                "COM5",
                115200,
                8,
                (SerialParity)99,
                SerialStopBits.One,
                SerialHandshake.None);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "parity",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_InvalidStopBits_ShouldThrow()
    {
        void Act()
        {
            _ = new SerialTransportOptions(
                "COM5",
                115200,
                8,
                SerialParity.None,
                (SerialStopBits)99,
                SerialHandshake.None);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "stopBits",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_InvalidHandshake_ShouldThrow()
    {
        void Act()
        {
            _ = new SerialTransportOptions(
                "COM5",
                115200,
                8,
                SerialParity.None,
                SerialStopBits.One,
                (SerialHandshake)99);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "handshake",
            exception.ParamName);
    }
}