namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies the mechanism that established an authenticated HASE
/// northbound client principal.
/// </summary>
public readonly record struct RuntimeHostAuthenticationMechanism
{
    /// <summary>
    /// Identifies mutual TLS client-certificate authentication.
    /// </summary>
    public static RuntimeHostAuthenticationMechanism MutualTls { get; } =
        new(
            "mutual-tls");

    /// <summary>
    /// Identifies the enforced trusted-loopback development profile.
    /// </summary>
    public static RuntimeHostAuthenticationMechanism TrustedLoopback { get; } =
        new(
            "trusted-loopback");

    /// <summary>
    /// Initializes one authentication-mechanism identifier.
    /// </summary>
    public RuntimeHostAuthenticationMechanism(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the normalized mechanism value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
