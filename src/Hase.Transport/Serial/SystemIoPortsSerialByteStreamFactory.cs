using System.IO.Ports;

namespace Hase.Transport.Serial;

/// <summary>
/// Opens serial byte streams through System.IO.Ports.
/// </summary>
public sealed class SystemIoPortsSerialByteStreamFactory
    : ISerialByteStreamFactory
{
    private readonly Func<
        SerialTransportOptions,
        ISystemIoPortsSerialPort> _serialPortFactory;

    /// <summary>
    /// Initializes the physical System.IO.Ports byte-stream factory.
    /// </summary>
    public SystemIoPortsSerialByteStreamFactory()
        : this(
            CreateSerialPort)
    {
    }

    internal SystemIoPortsSerialByteStreamFactory(
        Func<
            SerialTransportOptions,
            ISystemIoPortsSerialPort> serialPortFactory)
    {
        _serialPortFactory =
            serialPortFactory
            ?? throw new ArgumentNullException(
                nameof(serialPortFactory));
    }

    /// <inheritdoc />
    public ValueTask<ISerialByteStream> OpenAsync(
        SerialTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        cancellationToken.ThrowIfCancellationRequested();

        ISystemIoPortsSerialPort serialPort =
            _serialPortFactory(
                options)
            ?? throw new InvalidOperationException(
                "The System.IO.Ports serial-port factory returned null.");

        try
        {
            try
            {
                serialPort.Open();
            }
            catch (Exception exception)
                when (SerialPortOpenFailureClassifier.TryClassify(
                    exception,
                    out SerialPortOpenFailure failure))
            {
                throw new SerialPortOpenException(
                    options.PortName,
                    failure,
                    exception);
            }

            Stream stream =
                serialPort.BaseStream;

            if (!stream.CanRead)
            {
                throw new InvalidOperationException(
                    "The opened serial-port stream is not readable.");
            }

            if (!stream.CanWrite)
            {
                throw new InvalidOperationException(
                    "The opened serial-port stream is not writable.");
            }

            ISerialByteStream byteStream =
                new OwnedSerialPortByteStream(
                    serialPort,
                    stream);

            return ValueTask.FromResult(
                byteStream);
        }
        catch
        {
            serialPort.Dispose();

            throw;
        }
    }

    private static ISystemIoPortsSerialPort CreateSerialPort(
        SerialTransportOptions options)
    {
        var serialPort =
            new SerialPort(
                options.PortName,
                options.BaudRate,
                SystemIoPortsSerialSettingsMapper
                    .MapParity(
                        options.Parity),
                options.DataBits,
                SystemIoPortsSerialSettingsMapper
                    .MapStopBits(
                        options.StopBits))
            {
                Handshake =
                    SystemIoPortsSerialSettingsMapper
                        .MapHandshake(
                            options.Handshake)
            };

        return new SystemIoPortsSerialPort(
            serialPort);
    }

    private sealed class OwnedSerialPortByteStream
        : ISerialByteStream
    {
        private readonly ISystemIoPortsSerialPort _serialPort;
        private readonly Stream _stream;
        private bool _disposed;

        public OwnedSerialPortByteStream(
            ISystemIoPortsSerialPort serialPort,
            Stream stream)
        {
            _serialPort =
                serialPort;

            _stream =
                stream;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            return _stream.ReadAsync(
                buffer,
                cancellationToken);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            return _stream.WriteAsync(
                buffer,
                cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed =
                true;

            _serialPort.Dispose();

            return ValueTask.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);
        }
    }

    private sealed class SystemIoPortsSerialPort
        : ISystemIoPortsSerialPort
    {
        private readonly SerialPort _serialPort;

        public SystemIoPortsSerialPort(
            SerialPort serialPort)
        {
            _serialPort =
                serialPort;
        }

        public Stream BaseStream =>
            _serialPort.BaseStream;

        public void Open()
        {
            _serialPort.Open();
        }

        public void Dispose()
        {
            _serialPort.Dispose();
        }
    }
}

/// <summary>
/// Isolates the non-virtual System.IO.Ports.SerialPort API for deterministic
/// transport tests.
/// </summary>
internal interface ISystemIoPortsSerialPort
    : IDisposable
{
    Stream BaseStream
    {
        get;
    }

    void Open();
}