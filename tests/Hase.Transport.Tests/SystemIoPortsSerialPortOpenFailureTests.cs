using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class SystemIoPortsSerialPortOpenFailureTests
{
    [Theory]
    [InlineData(32, SerialPortOpenFailure.Busy)]
    [InlineData(2, SerialPortOpenFailure.Unavailable)]
    [InlineData(5, SerialPortOpenFailure.AccessDenied)]
    [InlineData(1167, SerialPortOpenFailure.Unavailable)]
    public async Task OpenAsync_ClassifiedOpenFailure_ShouldWrapAndDispose(
        int nativeErrorCode,
        SerialPortOpenFailure expectedFailure)
    {
        // Arrange
        var port =
            new ThrowingSerialPort(
                new TestIOException(
                    nativeErrorCode));

        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                _ => port);

        // Act
        async Task Act()
        {
            _ = await factory.OpenAsync(
                new SerialTransportOptions(
                    "COM10",
                    115200));
        }

        // Assert
        SerialPortOpenException exception =
            await Assert.ThrowsAsync<
                SerialPortOpenException>(
                    Act);

        Assert.Equal(
            "COM10",
            exception.PortName);

        Assert.Equal(
            expectedFailure,
            exception.Failure);

        Assert.Equal(
            1,
            port.DisposeCallCount);
    }

    [Fact]
    public async Task OpenAsync_UnsupportedOpenException_ShouldPreserveAndDispose()
    {
        // Arrange
        var expectedException =
            new NotSupportedException(
                "Unsupported open failure.");

        var port =
            new ThrowingSerialPort(
                expectedException);

        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                _ => port);

        // Act
        async Task Act()
        {
            _ = await factory.OpenAsync(
                new SerialTransportOptions(
                    "COM10",
                    115200));
        }

        // Assert
        NotSupportedException actualException =
            await Assert.ThrowsAsync<
                NotSupportedException>(
                    Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            1,
            port.DisposeCallCount);
    }

    private sealed class ThrowingSerialPort
        : ISystemIoPortsSerialPort
    {
        private readonly Exception _exception;

        public ThrowingSerialPort(
            Exception exception)
        {
            _exception =
                exception;
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public Stream BaseStream =>
            throw new InvalidOperationException(
                "The base stream must not be requested.");

        public void Open()
        {
            throw _exception;
        }

        public void Dispose()
        {
            DisposeCallCount++;
        }
    }

    private sealed class TestIOException
        : IOException
    {
        public TestIOException(
            int nativeErrorCode)
        {
            HResult =
                unchecked(
                    (int)0x80070000)
                | nativeErrorCode;
        }
    }
}