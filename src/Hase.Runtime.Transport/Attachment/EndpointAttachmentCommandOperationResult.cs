namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Represents the transport-independent result of an attachment-bound Command
/// operation.
/// </summary>
public sealed record EndpointAttachmentCommandOperationResult
{
    private EndpointAttachmentCommandOperationResult(
        EndpointAttachmentCommandOperationStatus status,
        object? returnValue,
        string? diagnostic)
    {
        Status =
            status;

        ReturnValue =
            returnValue;

        Diagnostic =
            string.IsNullOrWhiteSpace(
                diagnostic)
                ? null
                : diagnostic.Trim();
    }

    /// <summary>
    /// Gets the operation status.
    /// </summary>
    public EndpointAttachmentCommandOperationStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets whether the endpoint Command completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Status
        == EndpointAttachmentCommandOperationStatus.Success;

    /// <summary>
    /// Gets the optional endpoint-provided return value after success.
    /// </summary>
    public object? ReturnValue
    {
        get;
    }

    /// <summary>
    /// Gets optional safe diagnostic text.
    /// </summary>
    public string? Diagnostic
    {
        get;
    }

    /// <summary>
    /// Creates a successful endpoint Command result.
    /// </summary>
    public static EndpointAttachmentCommandOperationResult Successful(
        object? returnValue = null)
    {
        return new EndpointAttachmentCommandOperationResult(
            EndpointAttachmentCommandOperationStatus.Success,
            returnValue,
            diagnostic: null);
    }

    /// <summary>
    /// Creates an unsuccessful endpoint Command result.
    /// </summary>
    public static EndpointAttachmentCommandOperationResult Failed(
        EndpointAttachmentCommandOperationStatus status,
        string? diagnostic = null)
    {
        ValidateFailureStatus(
            status);

        return new EndpointAttachmentCommandOperationResult(
            status,
            returnValue: null,
            diagnostic);
    }

    private static void ValidateFailureStatus(
        EndpointAttachmentCommandOperationStatus status)
    {
        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The attachment Command operation status is not defined.");
        }

        if (status
            == EndpointAttachmentCommandOperationStatus.Success)
        {
            throw new ArgumentException(
                "A failed Command-operation result cannot have Success status.",
                nameof(status));
        }
    }
}