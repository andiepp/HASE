namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Identifies one external private-network configuration file.
/// </summary>
internal sealed record PrivateNetworkConfigurationFileArguments
{
    /// <summary>
    /// Initializes one validated external configuration reference.
    /// </summary>
    public PrivateNetworkConfigurationFileArguments(
        string configurationFilePath)
    {
        ArgumentNullException.ThrowIfNull(
            configurationFilePath);

        if (string.IsNullOrWhiteSpace(
                configurationFilePath)
            || !Path.IsPathFullyQualified(
                configurationFilePath))
        {
            throw new ArgumentException(
                "The private-network configuration file path must be fully "
                + "qualified.",
                nameof(configurationFilePath));
        }

        ConfigurationFilePath =
            Path.GetFullPath(
                configurationFilePath);
    }

    public string ConfigurationFilePath
    {
        get;
    }
}
