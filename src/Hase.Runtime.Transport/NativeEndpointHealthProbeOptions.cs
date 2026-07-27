namespace Hase.Runtime.Transport;

/// <summary>
/// Configures periodic protocol health probing for one native endpoint.
/// </summary>
public sealed record NativeEndpointHealthProbeOptions
{
    /// <summary>
    /// Gets the approved default native endpoint health-probe timing.
    /// </summary>
    public static NativeEndpointHealthProbeOptions Default
    {
        get;
    } =
        new(
            probeInterval:
                TimeSpan.FromSeconds(
                    1),
            probeTimeout:
                TimeSpan.FromSeconds(
                    3));

    /// <summary>
    /// Initializes native endpoint health-probe options.
    /// </summary>
    public NativeEndpointHealthProbeOptions(
        TimeSpan probeInterval,
        TimeSpan probeTimeout)
    {
        if (probeInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probeInterval),
                probeInterval,
                "The native endpoint probe interval must be positive.");
        }

        if (probeTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probeTimeout),
                probeTimeout,
                "The native endpoint probe timeout must be positive.");
        }

        ProbeInterval =
            probeInterval;

        ProbeTimeout =
            probeTimeout;
    }

    /// <summary>
    /// Gets the delay between successful native endpoint health probes.
    /// </summary>
    public TimeSpan ProbeInterval
    {
        get;
    }

    /// <summary>
    /// Gets the maximum duration of one native endpoint health probe.
    /// </summary>
    public TimeSpan ProbeTimeout
    {
        get;
    }
}
