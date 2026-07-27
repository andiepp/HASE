namespace Hase.Client;

/// <summary>
/// Represents one normalized remote Command execution result.
/// </summary>
public sealed record RemoteCommandOperationResult
{
    private RemoteCommandOperationResult(
        RemoteCommandOperationStatus status,
        RemoteValue? returnValue,
        string? diagnostic)
    {
        Status =
            status;
        ReturnValue =
            returnValue;
        Diagnostic =
            NormalizeDiagnostic(
                diagnostic);
    }

    /// <summary>
    /// Gets the normalized Command execution status.
    /// </summary>
    public RemoteCommandOperationStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets whether the Command completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Status
        == RemoteCommandOperationStatus.Success;

    /// <summary>
    /// Gets the optional endpoint-provided normalized return value after
    /// success.
    /// </summary>
    public RemoteValue? ReturnValue
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
    /// Creates one successful Command result.
    /// </summary>
    public static RemoteCommandOperationResult Successful(
        RemoteValue? returnValue = null)
    {
        return new RemoteCommandOperationResult(
            RemoteCommandOperationStatus.Success,
            returnValue,
            diagnostic: null);
    }

    /// <summary>
    /// Creates one failed Command result.
    /// </summary>
    public static RemoteCommandOperationResult Failed(
        RemoteCommandOperationStatus status,
        string? diagnostic = null)
    {
        ValidateFailureStatus(
            status);

        return new RemoteCommandOperationResult(
            status,
            returnValue: null,
            diagnostic);
    }

    private static void ValidateFailureStatus(
        RemoteCommandOperationStatus status)
    {
        if (!Enum.IsDefined(
                status)
            || status == RemoteCommandOperationStatus.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "A specified remote Command failure status is required.");
        }

        if (status == RemoteCommandOperationStatus.Success)
        {
            throw new ArgumentException(
                "A failed Command-operation result cannot have Success "
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
