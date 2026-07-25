namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies one semantic northbound operation permission.
/// </summary>
public readonly record struct RuntimeHostPermission
{
    /// <summary>
    /// Initializes one immutable semantic permission.
    /// </summary>
    public RuntimeHostPermission(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the stable permission value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the permission required to retrieve a runtime-host snapshot.
    /// </summary>
    public static RuntimeHostPermission ReadSnapshot { get; } =
        new("runtime-host.snapshot.read");

    /// <summary>
    /// Gets the permission required to read a cached Property value.
    /// </summary>
    public static RuntimeHostPermission ReadCachedProperty { get; } =
        new("property.cached.read");

    /// <summary>
    /// Gets the permission required to perform an authoritative Property read.
    /// </summary>
    public static RuntimeHostPermission ReadAuthoritativeProperty { get; } =
        new("property.authoritative.read");

    /// <summary>
    /// Gets the permission required to write a Property.
    /// </summary>
    public static RuntimeHostPermission WriteProperty { get; } =
        new("property.write");

    /// <summary>
    /// Gets the permission required to execute a Command.
    /// </summary>
    public static RuntimeHostPermission ExecuteCommand { get; } =
        new("command.execute");

    /// <summary>
    /// Gets the permission required to open an observation subscription.
    /// </summary>
    public static RuntimeHostPermission SubscribeObservation { get; } =
        new("observation.subscribe");

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
