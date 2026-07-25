namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies one individual credential used to authenticate a HASE
/// northbound client principal.
/// </summary>
public readonly record struct RuntimeHostClientCredentialId
{
    /// <summary>
    /// Initializes one client-credential identifier.
    /// </summary>
    public RuntimeHostClientCredentialId(
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
