namespace Hase.Client;

/// <summary>
/// Represents the normalized result of one authoritative remote Property read
/// or write.
/// </summary>
public sealed record RemotePropertyOperationResult
{
    private RemotePropertyOperationResult(
        RemotePropertyOperationStatus status,
        RemotePropertyValue? confirmedValue,
        string? diagnostic)
    {
        Status =
            status;
        ConfirmedValue =
            confirmedValue;
        Diagnostic =
            NormalizeDiagnostic(
                diagnostic);
    }

    /// <summary>
    /// Gets the normalized Property operation status.
    /// </summary>
    public RemotePropertyOperationStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets whether the authoritative operation completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Status
        == RemotePropertyOperationStatus.Success;

    /// <summary>
    /// Gets the endpoint-confirmed Property value after success.
    /// </summary>
    public RemotePropertyValue? ConfirmedValue
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
    /// Creates one successful authoritative Property-operation result.
    /// </summary>
    public static RemotePropertyOperationResult Successful(
        RemotePropertyValue confirmedValue)
    {
        ArgumentNullException.ThrowIfNull(
            confirmedValue);

        return new RemotePropertyOperationResult(
            RemotePropertyOperationStatus.Success,
            confirmedValue,
            diagnostic: null);
    }

    /// <summary>
    /// Creates one failed authoritative Property-operation result.
    /// </summary>
    public static RemotePropertyOperationResult Failed(
        RemotePropertyOperationStatus status,
        string? diagnostic = null)
    {
        ValidateFailureStatus(
            status);

        return new RemotePropertyOperationResult(
            status,
            confirmedValue: null,
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
                "A failed Property-operation result cannot have Success "
                + "status.",
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
