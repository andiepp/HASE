namespace Hase.Mcnf;

/// <summary>
/// Session-level framing limits and timing of one MCNF node connection.
/// </summary>
public sealed class McnfFramingOptions
{
    public McnfFramingOptions(
        TimeSpan totalExchangeTimeout,
        int nodeBufferSize,
        bool skipsLeadingText = false)
    {
        if (totalExchangeTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalExchangeTimeout));
        }

        if (nodeBufferSize < McnfConstants.HeaderSize + 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeBufferSize),
                nodeBufferSize,
                "The MCNF node buffer must hold at least a minimal framed request.");
        }

        TotalExchangeTimeout = totalExchangeTimeout;
        NodeBufferSize = nodeBufferSize;
        SkipsLeadingText = skipsLeadingText;
    }

    /// <summary>Gets the total timeout of one complete exchange.</summary>
    public TimeSpan TotalExchangeTimeout { get; }

    /// <summary>
    /// Gets the node's message buffer size in bytes. Request frames and
    /// expected responses must both fit into this buffer.
    /// </summary>
    public int NodeBufferSize { get; }

    /// <summary>
    /// Gets whether printable text arriving before a framed response is
    /// skipped instead of being taken for the response.
    /// </summary>
    /// <remarks>
    /// Some node firmware prints debug text on the host port before it
    /// answers. The first byte of a framed response is the error byte, a
    /// small code that is never printable (0x20-0x7E), so the text can be
    /// told apart. Line ends (0x0A, 0x0D) count as text only after printable
    /// text, so the error codes 10 and 13 are still recognized as the first
    /// byte. The connectivity test is never skipped, because its answer
    /// (0x21) is printable.
    /// </remarks>
    public bool SkipsLeadingText { get; }
}
