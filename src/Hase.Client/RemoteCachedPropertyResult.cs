namespace Hase.Client;

/// <summary>
/// Represents the normalized result of querying one cached remote
/// runtime-host Property.
/// </summary>
public sealed record RemoteCachedPropertyResult
{
    private RemoteCachedPropertyResult(
        RemotePropertyOperationStatus status,
        RemotePublishedPropertySnapshot? snapshot,
        string? diagnostic)
    {
        Status =
            status;
        Snapshot =
            snapshot;
        Diagnostic =
            NormalizeDiagnostic(
                diagnostic);
    }

    /// <summary>
    /// Gets the normalized cached-query status.
    /// </summary>
    public RemotePropertyOperationStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets whether the cached query completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Status
        == RemotePropertyOperationStatus.Success;

    /// <summary>
    /// Gets the immutable cached Property snapshot after success.
    /// </summary>
    public RemotePublishedPropertySnapshot? Snapshot
    {
        get;
    }

    /// <summary>
    /// Gets optional safe diagnostic text.
    /// </summary>
    /// <remarks>
    /// Consumers must not parse diagnostic text for program logic.
    /// </remarks>
    public string? Diagnostic
    {
        get;
    }

    /// <summary>
    /// Creates one successful cached Property result.
    /// </summary>
    public static RemoteCachedPropertyResult Successful(
        RemotePublishedPropertySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        return new RemoteCachedPropertyResult(
            RemotePropertyOperationStatus.Success,
            snapshot,
            diagnostic: null);
    }

    /// <summary>
    /// Creates one failed cached Property result.
    /// </summary>
    public static RemoteCachedPropertyResult Failed(
        RemotePropertyOperationStatus status,
        string? diagnostic = null)
    {
        ValidateFailureStatus(
            status);

        return new RemoteCachedPropertyResult(
            status,
            snapshot: null,
            diagnostic);
    }

    private static void ValidateFailureStatus(
        RemotePropertyOperationStatus status)
    {
        if (!Enum.IsDefined(
                status)
            || status == RemotePropertyOperationStatus.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "A specified remote Property failure status is required.");
        }

        if (status == RemotePropertyOperationStatus.Success)
        {
            throw new ArgumentException(
                "A failed cached Property result cannot have Success status.",
                nameof(status));
        }
    }

    private static string? NormalizeDiagnostic(
        string? diagnostic)
    {
        return string.IsNullOrWhiteSpace(
            diagnostic)
                ? null
                : diagnostic.Trim();
    }
}
