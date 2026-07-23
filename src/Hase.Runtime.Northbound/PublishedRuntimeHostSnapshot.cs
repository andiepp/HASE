namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents one immutable northbound snapshot of a HASE runtime host.
/// </summary>
/// <remarks>
/// Runtime-host identity is intentionally excluded until its persistence and
/// configuration source are defined.
/// </remarks>
public sealed record PublishedRuntimeHostSnapshot
{
    /// <summary>
    /// Initializes a published runtime-host snapshot.
    /// </summary>
    public PublishedRuntimeHostSnapshot(
        RuntimeHostApiVersion apiVersion,
        IEnumerable<PublishedRuntimeEndpointSnapshot> endpoints)
    {
        ArgumentNullException.ThrowIfNull(
            endpoints);

        PublishedRuntimeEndpointSnapshot[] endpointSnapshot =
            endpoints.ToArray();

        if (endpointSnapshot.Any(
                endpoint =>
                    endpoint is null))
        {
            throw new ArgumentException(
                "The endpoint snapshot collection must not contain null.",
                nameof(endpoints));
        }

        ApiVersion =
            apiVersion;

        Endpoints =
            Array.AsReadOnly(
                endpointSnapshot);
    }

    /// <summary>
    /// Gets the northbound API contract version represented by this snapshot.
    /// </summary>
    public RuntimeHostApiVersion ApiVersion
    {
        get;
    }

    /// <summary>
    /// Gets an immutable snapshot of the runtime host's currently published
    /// endpoint attachments.
    /// </summary>
    public IReadOnlyList<PublishedRuntimeEndpointSnapshot> Endpoints
    {
        get;
    }
}