namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Bridges the closed endpoint kinds of the version 1 composition format to
/// the provider identifiers that replaced them.
/// </summary>
/// <remarks>
/// This is the one place the base library still names the instrument
/// families, and it exists only so that a composition written before the
/// format opened still loads. It goes away once every installation is
/// migrated and the version 1 reader is removed.
/// </remarks>
internal static class DesktopRuntimeHostLegacyEndpointKinds
{
    private static readonly (string Kind, string ProviderId)[] Bridge =
    [
        ("NativeNetwork",
            DesktopRuntimeHostEndpointCompositionProfile.NativeNetworkProviderId),
        ("CompactSerial",
            DesktopRuntimeHostEndpointCompositionProfile.CompactSerialProviderId),
        ("Kel103Serial",
            DesktopRuntimeHostEndpointCompositionProfile.Kel103SerialProviderId),
        ("RfLabSerial",
            DesktopRuntimeHostEndpointCompositionProfile.RfLabSerialProviderId)
    ];

    /// <summary>
    /// Resolves the provider that supplies a version 1 endpoint kind.
    /// </summary>
    public static bool TryGetProviderId(
        string? kind,
        out string providerId)
    {
        foreach ((string candidate, string mapped) in Bridge)
        {
            if (StringComparer.Ordinal.Equals(candidate, kind))
            {
                providerId = mapped;
                return true;
            }
        }

        providerId = string.Empty;
        return false;
    }

    /// <summary>
    /// Resolves the version 1 endpoint kind a provider is written back as.
    /// </summary>
    public static bool TryGetKind(
        string? providerId,
        out string kind)
    {
        foreach ((string candidate, string mapped) in Bridge)
        {
            if (StringComparer.Ordinal.Equals(mapped, providerId))
            {
                kind = candidate;
                return true;
            }
        }

        kind = string.Empty;
        return false;
    }
}
