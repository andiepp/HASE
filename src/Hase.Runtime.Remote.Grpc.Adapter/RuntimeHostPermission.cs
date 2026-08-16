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

    /// <summary>
    /// Gets the permission required to open a diagnostic projection
    /// subscription.
    /// </summary>
    public static RuntimeHostPermission SubscribeDiagnostics { get; } =
        new("diagnostics.subscribe");

    /// <summary>
    /// Gets the permission required to discover sanitized media capabilities.
    /// </summary>
    public static RuntimeHostPermission ReadMediaCapabilities { get; } =
        new("media.capability.read");

    /// <summary>
    /// Gets the permission required to receive runtime-host video.
    /// </summary>
    public static RuntimeHostPermission ReceiveMediaVideo { get; } =
        new("media.video.receive");

    /// <summary>
    /// Gets the independent permission required to receive runtime-host audio.
    /// </summary>
    public static RuntimeHostPermission ReceiveMediaAudio { get; } =
        new("media.audio.receive");

    /// <summary>
    /// Gets the permission required to start a media session.
    /// </summary>
    public static RuntimeHostPermission StartMediaSession { get; } =
        new("media.session.start");

    /// <summary>
    /// Gets the permission required to exchange media negotiation messages.
    /// </summary>
    public static RuntimeHostPermission NegotiateMediaSession { get; } =
        new("media.session.negotiate");

    /// <summary>
    /// Gets the permission required to stop a caller-owned media session.
    /// </summary>
    public static RuntimeHostPermission StopMediaSession { get; } =
        new("media.session.stop");

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
