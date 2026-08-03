using System.Text;

namespace Hase.Scpi;

public sealed class ScpiTextResponseFramer
{
    private readonly int maximumResponseBytes;
    private readonly byte[] responseTerminator;
    private readonly byte[] payload;
    private int observedByteCount;
    private int payloadByteCount;
    private int matchedTerminatorByteCount;
    private bool responseTaken;
    private bool faulted;

    public ScpiTextResponseFramer(ScpiTextFramingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        maximumResponseBytes = options.MaximumResponseBytes;
        responseTerminator = ScpiTextTerminators.GetBytes(options.ResponseTerminator);
        payload = new byte[maximumResponseBytes];
    }

    public bool IsComplete { get; private set; }

    public void Append(ReadOnlySpan<byte> bytes)
    {
        ThrowIfUnavailable();

        if (IsComplete && !bytes.IsEmpty)
        {
            Fail("Response data was received after the configured terminator.");
        }

        for (var index = 0; index < bytes.Length; index++)
        {
            observedByteCount++;
            if (observedByteCount > maximumResponseBytes)
            {
                Fail("The response exceeded the configured maximum byte count.");
            }

            var value = bytes[index];
            if (matchedTerminatorByteCount == 0)
            {
                if (value == responseTerminator[0])
                {
                    matchedTerminatorByteCount = 1;
                    if (responseTerminator.Length == 1)
                    {
                        CompleteFrame(bytes, index);
                    }
                }
                else
                {
                    AppendPayloadByte(value);
                }
            }
            else if (value == responseTerminator[matchedTerminatorByteCount])
            {
                matchedTerminatorByteCount++;
                if (matchedTerminatorByteCount == responseTerminator.Length)
                {
                    CompleteFrame(bytes, index);
                }
            }
            else
            {
                Fail("The response contained an incomplete or invalid terminator sequence.");
            }
        }
    }

    public string Complete()
    {
        ThrowIfUnavailable();

        if (!IsComplete)
        {
            throw new InvalidOperationException("The response terminator has not been received.");
        }

        responseTaken = true;
        return Encoding.ASCII.GetString(payload, 0, payloadByteCount);
    }

    private void CompleteFrame(ReadOnlySpan<byte> bytes, int currentIndex)
    {
        IsComplete = true;
        if (currentIndex != bytes.Length - 1)
        {
            Fail("Response data was received after the configured terminator.");
        }
    }

    private void AppendPayloadByte(byte value)
    {
        if (value is < 0x20 or > 0x7E)
        {
            Fail("SCPI response text must contain printable ASCII characters only.");
        }

        payload[payloadByteCount++] = value;
    }

    private void ThrowIfUnavailable()
    {
        if (faulted)
        {
            throw new InvalidOperationException("The response framer is faulted.");
        }

        if (responseTaken)
        {
            throw new InvalidOperationException("The framed response has already been completed.");
        }
    }

    private void Fail(string message)
    {
        faulted = true;
        throw new InvalidDataException(message);
    }
}
