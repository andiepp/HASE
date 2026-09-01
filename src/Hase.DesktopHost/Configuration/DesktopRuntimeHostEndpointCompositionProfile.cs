namespace Hase.DesktopHost.Configuration;

/// <summary>
/// The endpoints one Desktop Runtime Host composes, each named by the
/// provider that supplies it.
/// </summary>
/// <remarks>
/// The collection is open: a composition may name a provider this library has
/// never heard of, and only that provider interprets its settings. The typed
/// views below are a transitional convenience for callers that still speak in
/// endpoint kinds, and they go away once every caller reads entries.
/// </remarks>
public sealed record DesktopRuntimeHostEndpointCompositionProfile
{
    private const int MaximumEndpointCount = 64;

    /// <summary>
    /// The closed-kind format every installed composition was written in.
    /// </summary>
    public const int LegacyFormatVersion = 1;

    /// <summary>
    /// The provider-keyed format a migrated composition is written in.
    /// </summary>
    public const int OpenFormatVersion = 2;

    /// <summary>
    /// The provider identifiers whose settings this library can still project
    /// into typed views.
    /// </summary>
    internal const string NativeNetworkProviderId = "native-network";
    internal const string CompactSerialProviderId = "compact-serial";
    internal const string Kel103SerialProviderId = "kel-103-serial";
    internal const string RfLabSerialProviderId = "rf-lab-serial";

    /// <summary>
    /// Composes a profile from provider-named endpoints.
    /// </summary>
    public DesktopRuntimeHostEndpointCompositionProfile(
        IEnumerable<DesktopRuntimeHostEndpointEntry> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        Endpoints = endpoints.ToArray();

        foreach (DesktopRuntimeHostEndpointEntry endpoint in Endpoints)
        {
            if (endpoint is null)
            {
                throw new ArgumentException(
                    "An endpoint entry must not be null.",
                    nameof(endpoints));
            }
        }

        if (Endpoints.Count is 0 or > MaximumEndpointCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endpoints),
                "An endpoint composition must contain between one and 64 endpoints.");
        }

        string? duplicateEndpointId = Endpoints
            .Select(endpoint => endpoint.ExpectedEndpointId)
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateEndpointId is not null)
        {
            throw new ArgumentException(
                $"Endpoint identity '{duplicateEndpointId}' occurs more than once.",
                nameof(endpoints));
        }

        NativeNetworkEndpoints = ForProvider(NativeNetworkProviderId)
            .Select(CreateNativeNetworkProfile)
            .ToArray();
        CompactSerialEndpoints = ForProvider(CompactSerialProviderId)
            .Select(CreateCompactSerialProfile)
            .ToArray();
        Kel103SerialEndpoints = ForProvider(Kel103SerialProviderId)
            .Select(CreateKel103SerialProfile)
            .ToArray();
        RfLabSerialEndpoints = ForProvider(RfLabSerialProviderId)
            .Select(CreateRfLabSerialProfile)
            .ToArray();
    }

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
        : this(
            CreateEntries(
                nativeNetworkEndpoints,
                compactSerialEndpoints,
                kel103SerialEndpoints,
                rfLabSerialEndpoints))
    {
    }

    /// <summary>
    /// Gets the composed endpoints, in composition order.
    /// </summary>
    public IReadOnlyList<DesktopRuntimeHostEndpointEntry> Endpoints { get; }

    /// <summary>
    /// Gets the format version this composition was read in, and will be
    /// written back in.
    /// </summary>
    /// <remarks>
    /// An edit preserves the version of the file it edited. Migration is
    /// the one operation that changes it, so a host that has not been
    /// migrated never has the open shape written underneath it.
    /// </remarks>
    public int FormatVersion { get; init; } = LegacyFormatVersion;

    public IReadOnlyList<DesktopRuntimeHostNativeNetworkEndpointProfile> NativeNetworkEndpoints { get; }
    public IReadOnlyList<DesktopRuntimeHostCompactSerialEndpointProfile> CompactSerialEndpoints { get; }
    public IReadOnlyList<DesktopRuntimeHostKel103SerialEndpointProfile> Kel103SerialEndpoints { get; }
    public IReadOnlyList<DesktopRuntimeHostRfLabSerialEndpointProfile> RfLabSerialEndpoints { get; }

    /// <summary>
    /// Returns the endpoints supplied by one provider, in composition order.
    /// </summary>
    public IEnumerable<DesktopRuntimeHostEndpointEntry> ForProvider(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        string trimmed = providerId.Trim();

        return Endpoints.Where(endpoint =>
            StringComparer.Ordinal.Equals(endpoint.ProviderId, trimmed));
    }

    public override string ToString() =>
        $"Desktop Runtime Host endpoint composition ({Endpoints.Count} endpoints)";

    private static IEnumerable<DesktopRuntimeHostEndpointEntry> CreateEntries(
        IEnumerable<DesktopRuntimeHostNativeNetworkEndpointProfile> nativeNetworkEndpoints,
        IEnumerable<DesktopRuntimeHostCompactSerialEndpointProfile> compactSerialEndpoints,
        IEnumerable<DesktopRuntimeHostKel103SerialEndpointProfile> kel103SerialEndpoints,
        IEnumerable<DesktopRuntimeHostRfLabSerialEndpointProfile> rfLabSerialEndpoints)
    {
        ArgumentNullException.ThrowIfNull(nativeNetworkEndpoints);
        ArgumentNullException.ThrowIfNull(compactSerialEndpoints);
        ArgumentNullException.ThrowIfNull(kel103SerialEndpoints);
        ArgumentNullException.ThrowIfNull(rfLabSerialEndpoints);

        return nativeNetworkEndpoints.Select(CreateNativeNetworkEntry)
            .Concat(compactSerialEndpoints.Select(CreateCompactSerialEntry))
            .Concat(kel103SerialEndpoints.Select(CreateKel103SerialEntry))
            .Concat(rfLabSerialEndpoints.Select(CreateRfLabSerialEntry));
    }

    internal static DesktopRuntimeHostEndpointEntry CreateNativeNetworkEntry(
        DesktopRuntimeHostNativeNetworkEndpointProfile endpoint) =>
        new(
            NativeNetworkProviderId,
            endpoint.ExpectedEndpointId,
            [
                new("host", endpoint.Host),
                new("port", Text(endpoint.Port))
            ]);

    internal static DesktopRuntimeHostEndpointEntry CreateCompactSerialEntry(
        DesktopRuntimeHostCompactSerialEndpointProfile endpoint) =>
        new(
            CompactSerialProviderId,
            endpoint.ExpectedEndpointId,
            [
                new("vendorId", Text(endpoint.VendorId)),
                new("productId", Text(endpoint.ProductId)),
                new("baudRate", Text(endpoint.BaudRate)),
                new(
                    "verificationTimeoutMilliseconds",
                    Text((int)endpoint.VerificationTimeout.TotalMilliseconds))
            ]);

    internal static DesktopRuntimeHostEndpointEntry CreateKel103SerialEntry(
        DesktopRuntimeHostKel103SerialEndpointProfile endpoint) =>
        new(
            Kel103SerialProviderId,
            endpoint.ExpectedEndpointId,
            SerialInstrumentSettings(
                endpoint.DefinitionReference.Id.Value,
                endpoint.DefinitionReference.Version,
                endpoint.SerialPort,
                endpoint.BaudRate));

    internal static DesktopRuntimeHostEndpointEntry CreateRfLabSerialEntry(
        DesktopRuntimeHostRfLabSerialEndpointProfile endpoint) =>
        new(
            RfLabSerialProviderId,
            endpoint.ExpectedEndpointId,
            SerialInstrumentSettings(
                endpoint.DefinitionReference.Id.Value,
                endpoint.DefinitionReference.Version,
                endpoint.SerialPort,
                endpoint.BaudRate));

    private static KeyValuePair<string, string>[] SerialInstrumentSettings(
        string definitionId,
        ushort definitionVersion,
        string serialPort,
        int baudRate) =>
        [
            new("definitionId", definitionId),
            new("definitionVersion", Text(definitionVersion)),
            new("serialPort", serialPort),
            new("baudRate", Text(baudRate))
        ];

    private static DesktopRuntimeHostNativeNetworkEndpointProfile
        CreateNativeNetworkProfile(DesktopRuntimeHostEndpointEntry endpoint) =>
        new(
            endpoint.ExpectedEndpointId,
            endpoint.RequireString("host"),
            endpoint.RequireInt32("port"));

    private static DesktopRuntimeHostCompactSerialEndpointProfile
        CreateCompactSerialProfile(DesktopRuntimeHostEndpointEntry endpoint) =>
        new(
            endpoint.ExpectedEndpointId,
            endpoint.RequireUInt16("vendorId"),
            endpoint.RequireUInt16("productId"),
            endpoint.RequireInt32("baudRate"),
            TimeSpan.FromMilliseconds(
                endpoint.RequireInt32("verificationTimeoutMilliseconds")));

    private static DesktopRuntimeHostKel103SerialEndpointProfile
        CreateKel103SerialProfile(DesktopRuntimeHostEndpointEntry endpoint) =>
        new(
            endpoint.ExpectedEndpointId,
            endpoint.RequireString("definitionId"),
            endpoint.RequireUInt16("definitionVersion"),
            endpoint.RequireString("serialPort"),
            endpoint.RequireInt32("baudRate"));

    private static DesktopRuntimeHostRfLabSerialEndpointProfile
        CreateRfLabSerialProfile(DesktopRuntimeHostEndpointEntry endpoint) =>
        new(
            endpoint.ExpectedEndpointId,
            endpoint.RequireString("definitionId"),
            endpoint.RequireUInt16("definitionVersion"),
            endpoint.RequireString("serialPort"),
            endpoint.RequireInt32("baudRate"));

    private static string Text(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Text(ushort value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
