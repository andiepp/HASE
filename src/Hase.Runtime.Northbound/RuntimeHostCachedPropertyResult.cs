namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents the normalized result of querying one cached runtime-host
/// Property.
/// </summary>
public sealed record RuntimeHostCachedPropertyResult
{
    private RuntimeHostCachedPropertyResult(
        RuntimeHostPropertyOperationStatus status,
        PublishedRuntimePropertySnapshot? snapshot,
        string? diagnostic)
    {
        Status =
            status;

        Snapshot =
            snapshot;

        Diagnostic =
            string.IsNullOrWhiteSpace(
                diagnostic)
                ? null
                : diagnostic.Trim();
    }

    /// <summary>
    /// Gets the normalized query status.
    /// </summary>
    public RuntimeHostPropertyOperationStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets whether the cached query completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Status
        == RuntimeHostPropertyOperationStatus.Success;

    /// <summary>
    /// Gets the immutable cached Property snapshot after success.
    /// </summary>
    public PublishedRuntimePropertySnapshot? Snapshot
    {
        get;
    }

    /// <summary>
    /// Gets optional safe diagnostic text. Applications must not parse this
    /// text for program logic.
    /// </summary>
    public string? Diagnostic
    {
        get;
    }

    /// <summary>
    /// Creates a successful cached Property result.
    /// </summary>
    public static RuntimeHostCachedPropertyResult Successful(
        PublishedRuntimePropertySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        return new RuntimeHostCachedPropertyResult(
            RuntimeHostPropertyOperationStatus.Success,
            snapshot,
            diagnostic: null);
    }

    /// <summary>
    /// Creates a failed cached Property result.
    /// </summary>
    public static RuntimeHostCachedPropertyResult Failed(
        RuntimeHostPropertyOperationStatus status,
        string? diagnostic = null)
    {
        ValidateFailureStatus(
            status);

        return new RuntimeHostCachedPropertyResult(
            status,
            snapshot: null,
            diagnostic);
    }

    private static void ValidateFailureStatus(
        RuntimeHostPropertyOperationStatus status)
    {
        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The runtime-host Property operation status is not defined.");
        }

        if (status
            == RuntimeHostPropertyOperationStatus.Success)
        {
            throw new ArgumentException(
                "A failed cached Property result cannot have Success status.",
                nameof(status));
        }
    }
}