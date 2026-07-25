namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies one stable HASE northbound client application independently
/// from any individual credential used to authenticate it.
/// </summary>
public readonly record struct RuntimeHostClientPrincipalId
{
    /// <summary>
    /// Initializes one client-principal identifier.
    /// </summary>
    public RuntimeHostClientPrincipalId(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the normalized identifier value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
