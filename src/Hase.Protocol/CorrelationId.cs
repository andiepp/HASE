namespace Hase.Protocol;

/// <summary>
/// Identifies a request/response exchange.
/// </summary>
public readonly record struct CorrelationId(uint Value)
{
    /// <summary>
    /// Represents the absence of a correlation identifier.
    /// </summary>
    public static CorrelationId None { get; } = new(0);

    /// <summary>
    /// Gets whether this instance represents no correlation.
    /// </summary>
    public bool IsNone => Value == 0;

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}