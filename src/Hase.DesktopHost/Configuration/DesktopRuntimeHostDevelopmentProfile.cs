using System.Net;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Defines the explicitly labeled certificate-free loopback development
/// profile for one Desktop Runtime Host. The northbound gRPC listener binds
/// to one validated loopback address without TLS and without client
/// certificates; every non-loopback deployment requires the secured
/// private-network configuration instead.
/// </summary>
public sealed record DesktopRuntimeHostDevelopmentProfile
{
    public DesktopRuntimeHostDevelopmentProfile(
        string identityFilePath,
        string loopbackAddress,
        int port,
        string? endpointCompositionFilePath = null,
        bool includeByteBufferSimulation = false,
        RuntimeDiagnosticLevel maximumDiagnosticLevel =
            RuntimeDiagnosticLevel.Operational)
    {
        IdentityFilePath = NormalizeFullyQualifiedPath(
            identityFilePath,
            nameof(identityFilePath),
            "development identity");
        EndpointCompositionFilePath = endpointCompositionFilePath is null
            ? null
            : NormalizeFullyQualifiedPath(
                endpointCompositionFilePath,
                nameof(endpointCompositionFilePath),
                "development endpoint composition");

        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        if (EndpointCompositionFilePath is not null
            && comparer.Equals(EndpointCompositionFilePath, IdentityFilePath))
        {
            throw new ArgumentException(
                "The development identity and endpoint-composition references "
                + "must use distinct files.",
                nameof(endpointCompositionFilePath));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            loopbackAddress,
            nameof(loopbackAddress));

        if (!IPAddress.TryParse(loopbackAddress, out IPAddress? parsedAddress))
        {
            throw new ArgumentException(
                "The development profile address is not a valid IP address.",
                nameof(loopbackAddress));
        }

        if (!IPAddress.IsLoopback(parsedAddress))
        {
            throw new ArgumentException(
                "The development profile is loopback-only and refuses every "
                + "non-loopback address. Non-loopback deployment requires the "
                + "secured private-network configuration.",
                nameof(loopbackAddress));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                "The development profile requires an explicit port between "
                + "1 and 65535.");
        }

        if (!Enum.IsDefined(maximumDiagnosticLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDiagnosticLevel));
        }

        if (EndpointCompositionFilePath is null && !includeByteBufferSimulation)
        {
            throw new ArgumentException(
                "The development profile requires an endpoint composition, "
                + "the byte-buffer simulation, or both.",
                nameof(includeByteBufferSimulation));
        }

        LoopbackAddress = parsedAddress;
        Port = port;
        IncludeByteBufferSimulation = includeByteBufferSimulation;
        MaximumDiagnosticLevel = maximumDiagnosticLevel;
    }

    public string IdentityFilePath { get; }
    public string? EndpointCompositionFilePath { get; }
    public IPAddress LoopbackAddress { get; }
    public int Port { get; }
    public bool IncludeByteBufferSimulation { get; }
    public RuntimeDiagnosticLevel MaximumDiagnosticLevel { get; }

    public string BindingDisplay =>
        $"http://{LoopbackAddress}:{Port}";

    public override string ToString() =>
        "Desktop Runtime Host development profile (loopback only, no TLS)";

    private static string NormalizeFullyQualifiedPath(
        string filePath,
        string parameterName,
        string role)
    {
        ArgumentNullException.ThrowIfNull(filePath, parameterName);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                $"The {role} file path must not be empty or whitespace.",
                parameterName);
        }

        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException(
                $"The {role} file path must be fully qualified.",
                parameterName);
        }

        return Path.GetFullPath(filePath);
    }
}
