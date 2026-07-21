namespace Hase.Runtime.Transport;

/// <summary>
/// Configures periodic health probing for one compact serial endpoint.
/// </summary>
internal sealed record CompactEndpointHealthProbeOptions
{
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

    public TimeSpan ProbeInterval
    {
        get;
    }

    public TimeSpan ProbeTimeout
    {
        get;
    }
}