namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostEndpointCompositionProfile
{
    private const int MaximumEndpointCount = 64;

    public DesktopRuntimeHostEndpointCompositionProfile(
        IEnumerable<DesktopRuntimeHostNativeNetworkEndpointProfile> nativeNetworkEndpoints,
        IEnumerable<DesktopRuntimeHostCompactSerialEndpointProfile> compactSerialEndpoints)
    {
        ArgumentNullException.ThrowIfNull(nativeNetworkEndpoints);
        ArgumentNullException.ThrowIfNull(compactSerialEndpoints);

        NativeNetworkEndpoints = nativeNetworkEndpoints.ToArray();
        CompactSerialEndpoints = compactSerialEndpoints.ToArray();

        if (NativeNetworkEndpoints.Count + CompactSerialEndpoints.Count is 0 or > MaximumEndpointCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nativeNetworkEndpoints),
                "An endpoint composition must contain between one and 64 endpoints.");
        }

        string? duplicateEndpointId = NativeNetworkEndpoints
            .Select(endpoint => endpoint.ExpectedEndpointId)
            .Concat(CompactSerialEndpoints.Select(endpoint => endpoint.ExpectedEndpointId))
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

    public override string ToString() =>
        $"Desktop Runtime Host endpoint composition ({NativeNetworkEndpoints.Count + CompactSerialEndpoints.Count} endpoints)";
}
