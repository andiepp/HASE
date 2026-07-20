using System.IO.Ports;
using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class SystemIoPortsSerialSettingsMapperTests
{
    [Theory]
    [InlineData(SerialParity.None, Parity.None)]
    [InlineData(SerialParity.Odd, Parity.Odd)]
    [InlineData(SerialParity.Even, Parity.Even)]
    [InlineData(SerialParity.Mark, Parity.Mark)]
    [InlineData(SerialParity.Space, Parity.Space)]
    public void MapParity_DefinedValue_ShouldMapExactly(
        SerialParity source,
        Parity expected)
    {
        Parity actual =
            SystemIoPortsSerialSettingsMapper
                .MapParity(
                    source);

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public void MapParity_UndefinedValue_ShouldThrow()
    {
        void Act()
        {
            _ = SystemIoPortsSerialSettingsMapper
                .MapParity(
                    (SerialParity)99);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "parity",
            exception.ParamName);
    }

    [Theory]
    [InlineData(SerialStopBits.One, StopBits.One)]
    [InlineData(SerialStopBits.OnePointFive, StopBits.OnePointFive)]
    [InlineData(SerialStopBits.Two, StopBits.Two)]
    public void MapStopBits_DefinedValue_ShouldMapExactly(
        SerialStopBits source,
        StopBits expected)
    {
        StopBits actual =
            SystemIoPortsSerialSettingsMapper
                .MapStopBits(
                    source);

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public void MapStopBits_UndefinedValue_ShouldThrow()
    {
        void Act()
        {
            _ = SystemIoPortsSerialSettingsMapper
                .MapStopBits(
                    (SerialStopBits)99);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "stopBits",
            exception.ParamName);
    }

    [Theory]
    [InlineData(SerialHandshake.None, Handshake.None)]
    [InlineData(SerialHandshake.XOnXOff, Handshake.XOnXOff)]
    [InlineData(SerialHandshake.RequestToSend, Handshake.RequestToSend)]
    [InlineData(
        SerialHandshake.RequestToSendXOnXOff,
        Handshake.RequestToSendXOnXOff)]
    public void MapHandshake_DefinedValue_ShouldMapExactly(
        SerialHandshake source,
        Handshake expected)
    {
        Handshake actual =
            SystemIoPortsSerialSettingsMapper
                .MapHandshake(
                    source);

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public void MapHandshake_UndefinedValue_ShouldThrow()
    {
        void Act()
        {
            _ = SystemIoPortsSerialSettingsMapper
                .MapHandshake(
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