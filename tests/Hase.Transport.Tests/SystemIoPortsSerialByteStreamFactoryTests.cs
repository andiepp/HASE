using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class SystemIoPortsSerialByteStreamFactoryTests
{
    [Fact]
    public async Task OpenAsync_ValidOptions_ShouldOpenAndReturnOwnedStream()
    {
        var port =
            new TestSerialPort();

        SerialTransportOptions? receivedOptions =
            null;

        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                options =>
                {
                    receivedOptions =
                        options;

                    return port;
                });

        var expectedOptions =
            new SerialTransportOptions(
                "COM5",
                115200);

        ISerialByteStream stream =
            await factory.OpenAsync(
                expectedOptions);

        Assert.Same(
            expectedOptions,
            receivedOptions);

        Assert.Equal(
            1,
            port.OpenCallCount);

        Assert.Equal(
            0,
            port.DisposeCallCount);

        await stream.DisposeAsync();

        Assert.Equal(
            1,
            port.DisposeCallCount);

        await stream.DisposeAsync();

        Assert.Equal(
            1,
            port.DisposeCallCount);
    }

    [Fact]
    public async Task OpenAsync_Result_ShouldReadAndWriteBaseStream()
    {
        byte[] expectedRead =
        [
            0x10,
            0x20
        ];

        var baseStream =
            new MemoryStream();

        await baseStream.WriteAsync(
            expectedRead);

        baseStream.Position =
            0;

        var port =
            new TestSerialPort(
                baseStream);

        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                _ => port);

        await using ISerialByteStream stream =
            await factory.OpenAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200));

        var readBuffer =
            new byte[4];

        int bytesRead =
            await stream.ReadAsync(
                readBuffer);

        Assert.Equal(
            expectedRead,
            readBuffer[..bytesRead]);

        port.Stream.Position =
            port.Stream.Length;

        byte[] expectedWrite =
        [
            0x30,
            0x40
        ];

        await stream.WriteAsync(
            expectedWrite);

        Assert.Equal(
            expectedRead.Concat(
                expectedWrite),
            port.Stream.ToArray());
    }

    [Fact]
    public async Task OpenAsync_OpenFailure_ShouldWrapAndDisposePort()
    {
        var expectedException =
            new IOException(
                "The serial port could not be opened.");

        var port =
            new TestSerialPort
            {
                OpenException =
                    expectedException
            };

        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                _ => port);

        async Task Act()
        {
            _ = await factory.OpenAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200));
        }

        SerialPortOpenException actualException =
            await Assert.ThrowsAsync<
                SerialPortOpenException>(
                    Act);

        Assert.Equal(
            "COM5",
            actualException.PortName);

        Assert.Equal(
            SerialPortOpenFailure.Failed,
            actualException.Failure);

        Assert.Same(
            expectedException,
            actualException.InnerException);

        Assert.Equal(
            1,
            port.DisposeCallCount);
    }

    [Fact]
    public async Task OpenAsync_BaseStreamFailure_ShouldDisposePort()
    {
        var port =
            new TestSerialPort
            {
                BaseStreamException =
                    new InvalidOperationException(
                        "No base stream is available.")
            };

        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                _ => port);

        async Task Act()
        {
            _ = await factory.OpenAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Equal(
            1,
            port.DisposeCallCount);
    }

    [Fact]
    public async Task OpenAsync_CancelledToken_ShouldNotCreatePort()
    {
        int factoryCallCount =
            0;

        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                _ =>
                {
                    factoryCallCount++;

                    return new TestSerialPort();
                });

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await factory.OpenAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200),
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            factoryCallCount);
    }

    [Fact]
    public async Task OpenAsync_NullOptions_ShouldThrow()
    {
        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                _ => new TestSerialPort());

        async Task Act()
        {
            _ = await factory.OpenAsync(
                null!);
        }

        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task OpenAsync_NullPort_ShouldThrow()
    {
        var factory =
            new SystemIoPortsSerialByteStreamFactory(
                _ => null!);

        async Task Act()
        {
            _ = await factory.OpenAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);
    }

    [Fact]
    public void Constructor_NullPortFactory_ShouldThrow()
    {
        void Act()
        {
            _ = new SystemIoPortsSerialByteStreamFactory(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private sealed class TestSerialPort
        : ISystemIoPortsSerialPort
    {
        private readonly MemoryStream _stream;

        public TestSerialPort(
            MemoryStream? stream = null)
        {
            _stream =
                stream
                ?? new MemoryStream();
        }

        public Exception? OpenException
        {
            get;
            init;
        }

        public Exception? BaseStreamException
        {
            get;
            init;
        }

        public int OpenCallCount
        {
            get;
            private set;
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public MemoryStream Stream =>
            _stream;

        public Stream BaseStream =>
            BaseStreamException is null
                ? _stream
                : throw BaseStreamException;

        public void Open()
        {
            OpenCallCount++;

            if (OpenException is not null)
            {
                throw OpenException;
            }
        }

        public void Dispose()
        {
            DisposeCallCount++;

            _stream.Dispose();
        }
    }
}