using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents the normalized result of an authoritative runtime-host Property
/// read or write.
/// </summary>
public sealed record RuntimeHostPropertyOperationResult
{
    private RuntimeHostPropertyOperationResult(
        RuntimeHostPropertyOperationStatus status,
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
    /// Gets the normalized operation status.
    /// </summary>
    public RuntimeHostPropertyOperationStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets whether the authoritative operation completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Status
        == RuntimeHostPropertyOperationStatus.Success;

    /// <summary>
    /// Gets the endpoint-confirmed Property value after success.
    /// </summary>
    public PropertyValue? ConfirmedValue
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
    /// Creates a successful authoritative Property-operation result.
    /// </summary>
    public static RuntimeHostPropertyOperationResult Successful(
        PropertyValue confirmedValue)
    {
        ArgumentNullException.ThrowIfNull(
            confirmedValue);

        return new RuntimeHostPropertyOperationResult(
            RuntimeHostPropertyOperationStatus.Success,
            confirmedValue,
            diagnostic: null);
    }

    /// <summary>
    /// Creates a failed authoritative Property-operation result.
    /// </summary>
    public static RuntimeHostPropertyOperationResult Failed(
        RuntimeHostPropertyOperationStatus status,
        string? diagnostic = null)
    {
        ValidateFailureStatus(
            status);

        return new RuntimeHostPropertyOperationResult(
            status,
            confirmedValue: null,
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
                "A failed Property-operation result cannot have Success status.",
                nameof(status));
        }
    }
}