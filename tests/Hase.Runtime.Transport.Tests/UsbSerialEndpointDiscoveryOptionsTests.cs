using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointDiscoveryOptionsTests
{
    [Fact]
    public void Constructor_DefaultSerialFormat_ShouldExposeSettings()
    {
        TimeSpan timeout =
            TimeSpan.FromSeconds(
                2);

        var options =
            new UsbSerialEndpointDiscoveryOptions(
                baudRate: 115200,
                verificationTimeout: timeout);

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

        Assert.Equal(
            timeout,
            options.VerificationTimeout);
    }

    [Fact]
    public void Constructor_ExplicitSerialFormat_ShouldExposeSettings()
    {
        var options =
            new UsbSerialEndpointDiscoveryOptions(
                baudRate: 57600,
                dataBits: 7,
                SerialParity.Even,
                SerialStopBits.Two,
                SerialHandshake.RequestToSend,
                verificationTimeout: TimeSpan.FromSeconds(
                    3));

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
            SerialHandshake.RequestToSend,
            options.Handshake);
    }

    [Fact]
    public void Constructor_NonPositiveBaudRate_ShouldThrow()
    {
        void Act()
        {
            _ = new UsbSerialEndpointDiscoveryOptions(
                baudRate: 0,
                verificationTimeout: TimeSpan.FromSeconds(
                    1));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Constructor_InvalidDataBits_ShouldThrow()
    {
        void Act()
        {
            _ = new UsbSerialEndpointDiscoveryOptions(
                baudRate: 115200,
                dataBits: 4,
                SerialParity.None,
                SerialStopBits.One,
                SerialHandshake.None,
                verificationTimeout: TimeSpan.FromSeconds(
                    1));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Constructor_NonPositiveVerificationTimeout_ShouldThrow()
    {
        void Act()
        {
            _ = new UsbSerialEndpointDiscoveryOptions(
                baudRate: 115200,
                verificationTimeout: TimeSpan.Zero);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void CreateTransportOptions_PortName_ShouldBeCandidateSpecific()
    {
        var options =
            new UsbSerialEndpointDiscoveryOptions(
                baudRate: 115200,
                dataBits: 7,
                SerialParity.Odd,
                SerialStopBits.Two,
                SerialHandshake.RequestToSend,
                verificationTimeout: TimeSpan.FromSeconds(
                    1));

        SerialTransportOptions transportOptions =
            options.CreateTransportOptions(
                "COM10");

        Assert.Equal(
            "COM10",
            transportOptions.PortName);

        Assert.Equal(
            options.BaudRate,
            transportOptions.BaudRate);

        Assert.Equal(
            options.DataBits,
            transportOptions.DataBits);

        Assert.Equal(
            options.Parity,
            transportOptions.Parity);

        Assert.Equal(
            options.StopBits,
            transportOptions.StopBits);

        Assert.Equal(
            options.Handshake,
            transportOptions.Handshake);
    }

    [Fact]
    public void CreateTransportOptions_EmptyPortName_ShouldThrow()
    {
        var options =
            new UsbSerialEndpointDiscoveryOptions(
                baudRate: 115200,
                verificationTimeout: TimeSpan.FromSeconds(
                    1));

        void Act()
        {
            _ = options.CreateTransportOptions(
                "");
        }

        Assert.Throws<ArgumentException>(
            Act);
    }
}