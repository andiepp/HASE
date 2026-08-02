using Hase.DesktopHost.App.Physical;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.App.Hosting;

public sealed record DesktopRuntimeHostProductionConfigurationPlan
{
    private DesktopRuntimeHostProductionConfigurationPlan(
        string identityFilePath,
        RuntimeHostId? configuredRuntimeHostId,
        DesktopRuntimeHostEndpointCompositionProfile endpointComposition,
        bool includeByteBufferSimulation)
    {
        IdentityFilePath = identityFilePath;
        ConfiguredRuntimeHostId = configuredRuntimeHostId;
        EndpointComposition = endpointComposition;
        ExpectedPublishedEndpointCount =
            endpointComposition.NativeNetworkEndpoints.Count
            + endpointComposition.CompactSerialEndpoints.Count
            + (includeByteBufferSimulation ? 1 : 0);
    }

    public string IdentityFilePath { get; }
    public RuntimeHostId? ConfiguredRuntimeHostId { get; }
    public DesktopRuntimeHostEndpointCompositionProfile EndpointComposition { get; }
    public int ExpectedPublishedEndpointCount { get; }

    public static DesktopRuntimeHostProductionConfigurationPlan Create(
        DesktopRuntimeHostStartupConfiguration configuration,
        string legacyIdentityFilePath,
        RuntimeHostId legacyRuntimeHostId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyIdentityFilePath);
        ArgumentNullException.ThrowIfNull(legacyRuntimeHostId);

        if (configuration.InstallationProfile is not null)
        {
            DesktopRuntimeHostEndpointCompositionProfile endpoints =
                configuration.EndpointCompositionProfile
                ?? throw new InvalidOperationException(
                    "Single-profile startup requires a loaded endpoint composition.");

            return new DesktopRuntimeHostProductionConfigurationPlan(
                configuration.InstallationProfile.IdentityFilePath,
                configuredRuntimeHostId: null,
                endpoints,
                configuration.IncludeByteBufferSimulation);
        }

        string? configuredEsp32Host = configuration.Esp32Host;
        string legacyEsp32Host =
            string.IsNullOrWhiteSpace(configuredEsp32Host)
                ? throw new InvalidOperationException(
                    "Legacy startup requires an ESP32 host.")
                : configuredEsp32Host;

        var legacyEndpoints = new DesktopRuntimeHostEndpointCompositionProfile(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    PhysicalEndpointIdentities.Esp32EndpointId.Value,
                    legacyEsp32Host,
                    5000)
            ],
            [
                new DesktopRuntimeHostCompactSerialEndpointProfile(
                    "arduino-uno-01",
                    0x2341,
                    0x0043,
                    115200,
                    TimeSpan.FromSeconds(3))
            ]);

        return new DesktopRuntimeHostProductionConfigurationPlan(
            legacyIdentityFilePath,
            legacyRuntimeHostId,
            legacyEndpoints,
            configuration.IncludeByteBufferSimulation);
    }
}
