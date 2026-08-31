using Hase.Mcnf;
using Hase.Transport.Serial;

namespace Hase.Mcnf.RfLab.Hosting.Tests;

internal static class RfLabHostingTestSupport
{
    public static SerialTransportOptions SupportedOptions() => new(
        "TEST-PORT",
        115200,
        dataBits: 8,
        SerialParity.None,
        SerialStopBits.One,
        SerialHandshake.None,
        assertDataTerminalReady: true,
        assertRequestToSend: true);

    public static byte[] ConnectivityResponse() => [0x21];

    public static byte[] SuccessResponse(params byte[] payload)
    {
        var frame = new byte[payload.Length + 2];
        payload.CopyTo(frame, 1);
        frame[^1] = McnfChecksum.Compute(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }

    public static byte[] NodeTypeResponse() => SuccessResponse(0xAE, 0x70, 0x10, 0x80);

    public static byte[] ConfigurationResponse(bool ledOn = false, bool si5351Present = true)
    {
        byte state = 0;
        if (ledOn)
        {
            state |= 0b01;
        }

        if (si5351Present)
        {
            state |= 0b10;
        }

        return SuccessResponse(0x00, 0x00, 0x00, state);
    }

    public static byte[] SensorResponse(int adcValue) =>
        SuccessResponse((byte)(adcValue / 256), (byte)(adcValue % 256));

    public static byte[] AcknowledgeResponse() => SuccessResponse();

    /// <summary>
    /// The complete scripted open sequence: connectivity test, node-type
    /// verification, configuration handshake, and one sensor reading.
    /// </summary>
    public static ScriptedSerialByteStream SuccessfulOpenStream(params byte[][] extra)
    {
        var responses = new List<byte[]>
        {
            ConnectivityResponse(),
            NodeTypeResponse(),
            ConfigurationResponse(),
            SensorResponse(0x029A)
        };
        responses.AddRange(extra);
        return new ScriptedSerialByteStream([.. responses]);
    }
}

internal sealed class RecordingSerialFactory : ISerialByteStreamFactory
{
    private readonly Queue<ISerialByteStream> streams;

    public RecordingSerialFactory(params ISerialByteStream[] streams)
    {
        this.streams = new Queue<ISerialByteStream>(streams);
    }

    public int OpenCount { get; private set; }

    public List<SerialTransportOptions> OpenedOptions { get; } = [];

    public ValueTask<ISerialByteStream> OpenAsync(
        SerialTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenCount++;
        OpenedOptions.Add(options);
        if (!streams.TryDequeue(out ISerialByteStream? stream))
        {
            throw new InvalidOperationException(
                "The recording serial factory has no further scripted stream.");
        }

        return ValueTask.FromResult(stream);
    }
}

internal sealed class ScriptedSerialByteStream(params byte[][] responses) : ISerialByteStream
{
    private readonly Queue<byte[]> remaining = new(responses);
    private byte[]? current;
    private int offset;

    public List<byte[]> Writes { get; } = [];

    public int DisposeCount { get; private set; }

    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (current is null || offset >= current.Length)
        {
            if (!remaining.TryDequeue(out current))
            {
                return ValueTask.FromResult(0);
            }

            offset = 0;
        }

        int count = Math.Min(buffer.Length, current.Length - offset);
        current.AsSpan(offset, count).CopyTo(buffer.Span);
        offset += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Writes.Add(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    public static DateTimeOffset Timestamp { get; } =
        new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Timestamp;
}
