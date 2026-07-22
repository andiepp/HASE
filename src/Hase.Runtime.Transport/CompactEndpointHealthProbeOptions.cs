namespace Hase.Runtime.Transport;

/// <summary>
/// Configures periodic health probing for one compact serial endpoint.
/// </summary>
public sealed record CompactEndpointHealthProbeOptions
{
    /// <summary>
    /// Gets the approved default compact endpoint health-probe timing.
    /// </summary>
    public static CompactEndpointHealthProbeOptions Default
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
    /// Initializes compact endpoint health-probe options.
    /// </summary>
    public CompactEndpointHealthProbeOptions(
        TimeSpan probeInterval,
        TimeSpan probeTimeout)
    {
        if (probeInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probeInterval),
                probeInterval,
                "The compact endpoint probe interval must be positive.");
        }

        if (probeTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probeTimeout),
                probeTimeout,
                "The compact endpoint probe timeout must be positive.");
        }

        ProbeInterval =
            probeInterval;

        ProbeTimeout =
            probeTimeout;
    }

    /// <summary>
    /// Gets the delay between successful compact endpoint health probes.
    /// </summary>
    public TimeSpan ProbeInterval
    {
        get;
    }

    /// <summary>
    /// Gets the maximum duration of one compact endpoint health probe.
    /// </summary>
    public TimeSpan ProbeTimeout
    {
        get;
    }
}