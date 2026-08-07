using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Defines installation-wide external configuration references and startup
/// diagnostics for one Desktop Runtime Host.
/// </summary>
public sealed record DesktopRuntimeHostInstallationProfile
{
    public DesktopRuntimeHostInstallationProfile(
        string identityFilePath,
        string privateNetworkConfigurationFilePath,
        RuntimeDiagnosticLevel maximumDiagnosticLevel = RuntimeDiagnosticLevel.Operational,
        bool includeByteBufferSimulation = false,
        bool remoteDiagnosticsEnabled = false,
        RuntimeDiagnosticLevel remoteDiagnosticsMaximumLevel =
            RuntimeDiagnosticLevel.Operational,
        string? authorizationPolicyFilePath = null)
        : this(
            identityFilePath,
            privateNetworkConfigurationFilePath,
            Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(privateNetworkConfigurationFilePath))!,
                "desktop-runtime-endpoints.json"),
            maximumDiagnosticLevel,
            includeByteBufferSimulation,
            remoteDiagnosticsEnabled,
            remoteDiagnosticsMaximumLevel,
            authorizationPolicyFilePath)
    {
    }

    public DesktopRuntimeHostInstallationProfile(
        string identityFilePath,
        string privateNetworkConfigurationFilePath,
        string endpointCompositionFilePath,
        RuntimeDiagnosticLevel maximumDiagnosticLevel = RuntimeDiagnosticLevel.Operational,
        bool includeByteBufferSimulation = false,
        bool remoteDiagnosticsEnabled = false,
        RuntimeDiagnosticLevel remoteDiagnosticsMaximumLevel =
            RuntimeDiagnosticLevel.Operational,
        string? authorizationPolicyFilePath = null)
    {
        IdentityFilePath = NormalizeFullyQualifiedPath(identityFilePath, nameof(identityFilePath), "installation identity");
        PrivateNetworkConfigurationFilePath = NormalizeFullyQualifiedPath(
            privateNetworkConfigurationFilePath,
            nameof(privateNetworkConfigurationFilePath),
            "private-network deployment configuration");
        EndpointCompositionFilePath = NormalizeFullyQualifiedPath(
            endpointCompositionFilePath,
            nameof(endpointCompositionFilePath),
            "endpoint composition");
        AuthorizationPolicyFilePath = authorizationPolicyFilePath is null
            ? null
            : NormalizeFullyQualifiedPath(
                authorizationPolicyFilePath,
                nameof(authorizationPolicyFilePath),
                "authorization policy");

        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        if (comparer.Equals(
                IdentityFilePath,
                PrivateNetworkConfigurationFilePath))
        {
            throw new ArgumentException(
                "The installation identity and private-network deployment configuration must use distinct files.",
                nameof(privateNetworkConfigurationFilePath));
        }

        if (comparer.Equals(EndpointCompositionFilePath, IdentityFilePath)
            || comparer.Equals(EndpointCompositionFilePath, PrivateNetworkConfigurationFilePath))
        {
            throw new ArgumentException(
                "The endpoint-composition reference must use a distinct file.",
                nameof(endpointCompositionFilePath));
        }

        if (AuthorizationPolicyFilePath is not null
            && (comparer.Equals(AuthorizationPolicyFilePath, IdentityFilePath)
                || comparer.Equals(
                    AuthorizationPolicyFilePath,
                    PrivateNetworkConfigurationFilePath)
                || comparer.Equals(
                    AuthorizationPolicyFilePath,
                    EndpointCompositionFilePath)))
        {
            throw new ArgumentException(
                "The authorization-policy reference must use a distinct file.",
                nameof(authorizationPolicyFilePath));
        }

        if (!Enum.IsDefined(maximumDiagnosticLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDiagnosticLevel));
        }

        if (!Enum.IsDefined(remoteDiagnosticsMaximumLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(remoteDiagnosticsMaximumLevel));
        }

        if (remoteDiagnosticsEnabled
            && remoteDiagnosticsMaximumLevel > maximumDiagnosticLevel)
        {
            throw new ArgumentException(
                "Remote diagnostic projection cannot exceed the local "
                + "diagnostic capture level.",
                nameof(remoteDiagnosticsMaximumLevel));
        }
        if (remoteDiagnosticsEnabled && AuthorizationPolicyFilePath is null)
        {
            throw new ArgumentException(
                "Remote diagnostics require an explicit authorization-policy file.",
                nameof(authorizationPolicyFilePath));
        }

        MaximumDiagnosticLevel = maximumDiagnosticLevel;
        IncludeByteBufferSimulation = includeByteBufferSimulation;
        RemoteDiagnosticsEnabled = remoteDiagnosticsEnabled;
        RemoteDiagnosticsMaximumLevel = remoteDiagnosticsMaximumLevel;
    }

    public string IdentityFilePath { get; }
    public string PrivateNetworkConfigurationFilePath { get; }
    public string EndpointCompositionFilePath { get; }
    public RuntimeDiagnosticLevel MaximumDiagnosticLevel { get; }
    public bool IncludeByteBufferSimulation { get; }
    public bool RemoteDiagnosticsEnabled { get; }
    public RuntimeDiagnosticLevel RemoteDiagnosticsMaximumLevel { get; }
    public string? AuthorizationPolicyFilePath { get; }

    public override string ToString() => "Desktop Runtime Host installation profile";

    private static string NormalizeFullyQualifiedPath(string filePath, string parameterName, string role)
    {
        ArgumentNullException.ThrowIfNull(filePath, parameterName);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException($"The {role} file path must not be empty or whitespace.", parameterName);
        }

        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException($"The {role} file path must be fully qualified.", parameterName);
        }

        return Path.GetFullPath(filePath);
    }
}
