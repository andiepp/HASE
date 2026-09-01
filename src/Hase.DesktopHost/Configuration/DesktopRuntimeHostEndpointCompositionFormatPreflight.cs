namespace Hase.DesktopHost.Configuration;

/// <summary>
/// One endpoint as reported by the format preflight.
/// </summary>
/// <remarks>
/// Settings are counted rather than reported. A composition names serial
/// targets and hosts, and a preflight exists to be pasted into a handoff.
/// </remarks>
public sealed record DesktopRuntimeHostEndpointCompositionFormatEndpoint(
    string ProviderId,
    string ExpectedEndpointId,
    int SettingCount);

/// <summary>
/// What one installed composition is, and what migrating it would do.
/// </summary>
public sealed record DesktopRuntimeHostEndpointCompositionFormatAssessment
{
    public DesktopRuntimeHostEndpointCompositionFormatAssessment(
        int formatVersion,
        bool migrationRequired,
        bool expressibleInLegacyFormat,
        IReadOnlyList<DesktopRuntimeHostEndpointCompositionFormatEndpoint> endpoints)
    {
        FormatVersion = formatVersion;
        MigrationRequired = migrationRequired;
        ExpressibleInLegacyFormat = expressibleInLegacyFormat;
        Endpoints = endpoints
            ?? throw new ArgumentNullException(nameof(endpoints));
    }

    /// <summary>
    /// Gets the format version the composition is written in.
    /// </summary>
    public int FormatVersion { get; }

    /// <summary>
    /// Indicates whether migrating this composition would change it.
    /// </summary>
    public bool MigrationRequired { get; }

    /// <summary>
    /// Indicates whether every endpoint could still be written in the closed
    /// format, which is what an unmigrated host can read.
    /// </summary>
    public bool ExpressibleInLegacyFormat { get; }

    /// <summary>
    /// Gets the endpoints this composition names, in composition order.
    /// </summary>
    public IReadOnlyList<DesktopRuntimeHostEndpointCompositionFormatEndpoint>
        Endpoints
    { get; }

    /// <summary>
    /// Gets the number of endpoints this composition names.
    /// </summary>
    public int EndpointCount => Endpoints.Count;
}

/// <summary>
/// Reports what a composition-format migration would do, without doing it.
/// </summary>
/// <remarks>
/// This reads and reports. It creates no file, writes no file, and takes no
/// backup, so it is safe to run on a host that is running.
/// </remarks>
public static class DesktopRuntimeHostEndpointCompositionFormatPreflight
{
    public static async Task<DesktopRuntimeHostEndpointCompositionFormatAssessment>
        InspectAsync(
            string profilePath,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);

        DesktopRuntimeHostEndpointCompositionProfile profile =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                    profilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        return Assess(profile);
    }

    /// <summary>
    /// Assesses a composition already in hand.
    /// </summary>
    public static DesktopRuntimeHostEndpointCompositionFormatAssessment Assess(
        DesktopRuntimeHostEndpointCompositionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        bool expressibleInLegacyFormat = profile.Endpoints.All(endpoint =>
            DesktopRuntimeHostLegacyEndpointKinds.TryGetKind(
                endpoint.ProviderId,
                out _));

        return new DesktopRuntimeHostEndpointCompositionFormatAssessment(
            profile.FormatVersion,
            profile.FormatVersion
                != DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion,
            expressibleInLegacyFormat,
            profile.Endpoints
                .Select(endpoint =>
                    new DesktopRuntimeHostEndpointCompositionFormatEndpoint(
                        endpoint.ProviderId,
                        endpoint.ExpectedEndpointId,
                        endpoint.Settings.Count))
                .ToArray());
    }
}
