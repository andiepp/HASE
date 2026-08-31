using System.IO.Ports;

namespace Hase.Transport.Serial;

/// <summary>
/// Opens serial byte streams through System.IO.Ports.
/// </summary>
public sealed class SystemIoPortsSerialByteStreamFactory
    : ISerialByteStreamFactory
{
    /// <summary>
    /// Interval between two checks for buffered inbound bytes while no read
    /// can be issued.
    /// </summary>
    /// <remarks>
    /// The effective wait is the operating system timer granularity, which is
    /// approximately 15 ms on Windows.
    /// </remarks>
    private static readonly TimeSpan ReadPollingInterval =
        TimeSpan.FromMilliseconds(1);

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

        if (options.AssertDataTerminalReady)
        {
            serialPort.DtrEnable =
                true;
        }

        // Assigning RtsEnable throws while hardware flow control owns the
        // line, so the line is only touched when an assertion is requested.
        if (options.AssertRequestToSend)
        {
            serialPort.RtsEnable =
                true;
        }

        return new SystemIoPortsSerialPort(
            serialPort);
    }

    /// <summary>
    /// Owns one opened serial port and serializes its transfers.
    /// </summary>
    /// <remarks>
    /// A read is issued only while the port reports buffered bytes, and a
    /// transfer gate keeps reads and writes mutually exclusive. Some USB
    /// serial adapters abort a pending overlapped read as soon as a write is
    /// issued on the same handle, which leaves the stream unusable. Both
    /// measures together guarantee that no read is outstanding while a write
    /// is in progress.
    /// </remarks>
    private sealed class OwnedSerialPortByteStream
        : ISerialByteStream
    {
        private readonly ISystemIoPortsSerialPort _serialPort;
        private readonly Stream _stream;
        private readonly SemaphoreSlim _transferGate =
            new(
                1,
                1);

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

        public async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            while (true)
            {
                ThrowIfDisposed();

                await _transferGate.WaitAsync(
                    cancellationToken);

                try
                {
                    ThrowIfDisposed();

                    if (_serialPort.BytesToRead > 0)
                    {
                        return await _stream.ReadAsync(
                            buffer,
                            cancellationToken);
                    }
                }
                finally
                {
                    _transferGate.Release();
                }

                await Task.Delay(
                    ReadPollingInterval,
                    cancellationToken);
            }
        }

        public async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            await _transferGate.WaitAsync(
                cancellationToken);

            try
            {
                ThrowIfDisposed();

                await _stream.WriteAsync(
                    buffer,
                    cancellationToken);
            }
            finally
            {
                _transferGate.Release();
            }
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

        public int BytesToRead =>
            _serialPort.BytesToRead;

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

    /// <summary>
    /// Gets the number of bytes currently buffered for reading.
    /// </summary>
    int BytesToRead
    {
        get;
    }

    void Open();
}