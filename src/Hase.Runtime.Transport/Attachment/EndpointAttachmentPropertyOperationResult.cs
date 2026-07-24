using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Represents the transport-independent result of an attachment-bound
/// Property operation.
/// </summary>
public sealed record EndpointAttachmentPropertyOperationResult
{
    private EndpointAttachmentPropertyOperationResult(
        EndpointAttachmentPropertyOperationStatus status,
        PropertyValue? confirmedValue,
        string? diagnostic)
    {
        Status =
            status;

        ConfirmedValue =
            confirmedValue;

        Diagnostic =
            string.IsNullOrWhiteSpace(
                diagnostic)
                ? null
                : diagnostic.Trim();
    }

    /// <summary>
    /// Gets the operation status.
    /// </summary>
    public EndpointAttachmentPropertyOperationStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets whether the endpoint operation completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Status
        == EndpointAttachmentPropertyOperationStatus.Success;

    /// <summary>
    /// Gets the endpoint-confirmed Property value after success.
    /// </summary>
    public PropertyValue? ConfirmedValue
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
    /// Creates a successful endpoint operation result.
    /// </summary>
    public static EndpointAttachmentPropertyOperationResult Successful(
        PropertyValue confirmedValue)
    {
        ArgumentNullException.ThrowIfNull(
            confirmedValue);

        return new EndpointAttachmentPropertyOperationResult(
            EndpointAttachmentPropertyOperationStatus.Success,
            confirmedValue,
            diagnostic: null);
    }

    /// <summary>
    /// Creates an unsuccessful endpoint operation result.
    /// </summary>
    public static EndpointAttachmentPropertyOperationResult Failed(
        EndpointAttachmentPropertyOperationStatus status,
        string? diagnostic = null)
    {
        ValidateFailureStatus(
            status);

        return new EndpointAttachmentPropertyOperationResult(
            status,
            confirmedValue: null,
            diagnostic);
    }

    private static void ValidateFailureStatus(
        EndpointAttachmentPropertyOperationStatus status)
    {
        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The attachment Property operation status is not defined.");
        }

        if (status
            == EndpointAttachmentPropertyOperationStatus.Success)
        {
            throw new ArgumentException(
                "A failed Property-operation result cannot have Success status.",
                nameof(status));
        }
    }
}