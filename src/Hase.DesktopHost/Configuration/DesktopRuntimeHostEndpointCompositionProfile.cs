namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostEndpointCompositionProfile
{
    private const int MaximumEndpointCount = 64;

    public DesktopRuntimeHostEndpointCompositionProfile(
        IEnumerable<DesktopRuntimeHostNativeNetworkEndpointProfile> nativeNetworkEndpoints,
        IEnumerable<DesktopRuntimeHostCompactSerialEndpointProfile> compactSerialEndpoints)
        : this(
            nativeNetworkEndpoints,
            compactSerialEndpoints,
            Array.Empty<DesktopRuntimeHostKel103SerialEndpointProfile>())
    {
    }

    public DesktopRuntimeHostEndpointCompositionProfile(
        IEnumerable<DesktopRuntimeHostNativeNetworkEndpointProfile> nativeNetworkEndpoints,
        IEnumerable<DesktopRuntimeHostCompactSerialEndpointProfile> compactSerialEndpoints,
        IEnumerable<DesktopRuntimeHostKel103SerialEndpointProfile> kel103SerialEndpoints)
        : this(
            nativeNetworkEndpoints,
            compactSerialEndpoints,
            kel103SerialEndpoints,
            Array.Empty<DesktopRuntimeHostRfLabSerialEndpointProfile>())
    {
    }

    public DesktopRuntimeHostEndpointCompositionProfile(
        IEnumerable<DesktopRuntimeHostNativeNetworkEndpointProfile> nativeNetworkEndpoints,
        IEnumerable<DesktopRuntimeHostCompactSerialEndpointProfile> compactSerialEndpoints,
        IEnumerable<DesktopRuntimeHostKel103SerialEndpointProfile> kel103SerialEndpoints,
        IEnumerable<DesktopRuntimeHostRfLabSerialEndpointProfile> rfLabSerialEndpoints)
    {
        ArgumentNullException.ThrowIfNull(nativeNetworkEndpoints);
        ArgumentNullException.ThrowIfNull(compactSerialEndpoints);
        ArgumentNullException.ThrowIfNull(kel103SerialEndpoints);
        ArgumentNullException.ThrowIfNull(rfLabSerialEndpoints);

        NativeNetworkEndpoints = nativeNetworkEndpoints.ToArray();
        CompactSerialEndpoints = compactSerialEndpoints.ToArray();
        Kel103SerialEndpoints = kel103SerialEndpoints.ToArray();
        RfLabSerialEndpoints = rfLabSerialEndpoints.ToArray();

        if (NativeNetworkEndpoints.Count + CompactSerialEndpoints.Count
            + Kel103SerialEndpoints.Count + RfLabSerialEndpoints.Count
            is 0 or > MaximumEndpointCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nativeNetworkEndpoints),
                "An endpoint composition must contain between one and 64 endpoints.");
        }

        string? duplicateEndpointId = NativeNetworkEndpoints
            .Select(endpoint => endpoint.ExpectedEndpointId)
            .Concat(CompactSerialEndpoints.Select(endpoint => endpoint.ExpectedEndpointId))
            .Concat(Kel103SerialEndpoints.Select(endpoint => endpoint.ExpectedEndpointId))
            .Concat(RfLabSerialEndpoints.Select(endpoint => endpoint.ExpectedEndpointId))
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateEndpointId is not null)
        {
            throw new ArgumentException(
                $"Endpoint identity '{duplicateEndpointId}' occurs more than once.",
                nameof(nativeNetworkEndpoints));
        }
    }

    public IReadOnlyList<DesktopRuntimeHostNativeNetworkEndpointProfile> NativeNetworkEndpoints { get; }
    public IReadOnlyList<DesktopRuntimeHostCompactSerialEndpointProfile> CompactSerialEndpoints { get; }
    public IReadOnlyList<DesktopRuntimeHostKel103SerialEndpointProfile> Kel103SerialEndpoints { get; }
    public IReadOnlyList<DesktopRuntimeHostRfLabSerialEndpointProfile> RfLabSerialEndpoints { get; }

    public override string ToString() =>
        $"Desktop Runtime Host endpoint composition ({NativeNetworkEndpoints.Count + CompactSerialEndpoints.Count + Kel103SerialEndpoints.Count + RfLabSerialEndpoints.Count} endpoints)";
}
