namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Defines the configured physical ESP32 endpoint used by authenticated
/// northbound observation validation.
/// </summary>
internal sealed record CapabilityC034Arguments
{
    /// <summary>
    /// Initializes the C-034 physical validation arguments.
    /// </summary>
    public CapabilityC034Arguments(
        string endpointHost)
    {
        if (string.IsNullOrWhiteSpace(
                endpointHost))
        {
            throw new ArgumentException(
                "An ESP32 host name or IP address is required.",
                nameof(endpointHost));
        }

        EndpointHost =
            endpointHost;
    }

    /// <summary>
    /// Gets the configured physical ESP32 host name or IP address.
    /// </summary>
    public string EndpointHost
    {
        get;
    }
}
