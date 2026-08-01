using Hase.Client.Configuration;

namespace Hase.Client.Grpc.Configuration;

/// <summary>
/// Associates one transport-independent runtime-host profile with its external
/// private-network client configuration.
/// </summary>
public sealed record PrivateNetworkRuntimeHostProfile
{
    public PrivateNetworkRuntimeHostProfile(
        RuntimeHostProfile profile,
        string privateNetworkConfigurationFilePath)
    {
        Profile =
            profile
            ?? throw new ArgumentNullException(
                nameof(profile));

        ArgumentNullException.ThrowIfNull(
            privateNetworkConfigurationFilePath);

        if (string.IsNullOrWhiteSpace(
                privateNetworkConfigurationFilePath))
        {
            throw new ArgumentException(
                "The private-network client configuration file path must not be empty or whitespace.",
                nameof(privateNetworkConfigurationFilePath));
        }

        if (!Path.IsPathFullyQualified(
                privateNetworkConfigurationFilePath))
        {
            throw new ArgumentException(
                "The private-network client configuration file path must be fully qualified.",
                nameof(privateNetworkConfigurationFilePath));
        }

        PrivateNetworkConfigurationFilePath =
            Path.GetFullPath(
                privateNetworkConfigurationFilePath);
    }

    public RuntimeHostProfile Profile
    {
        get;
    }

    public string PrivateNetworkConfigurationFilePath
    {
        get;
    }

    public override string ToString() =>
        Profile.ProfileId.Value;
}
