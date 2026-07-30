using System.Collections.ObjectModel;

namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Describes one structured diagnostic event before process-local sequencing.
/// </summary>
public sealed class RuntimeDiagnosticEvent
{
    private static readonly IReadOnlyDictionary<string, string> EmptyDetails =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>());

    public RuntimeDiagnosticEvent(
        RuntimeDiagnosticLevel level,
        RuntimeDiagnosticCategory category,
        string eventName,
        RuntimeDiagnosticSeverity severity =
            RuntimeDiagnosticSeverity.Information,
        string? endpointId = null,
        Guid? attachmentGeneration = null,
        RuntimeDiagnosticDirection? direction = null,
        Guid? operationId = null,
        TimeSpan? duration = null,
        RuntimeDiagnosticOutcome? outcome = null,
        IReadOnlyDictionary<string, string>? details = null,
        RuntimeDiagnosticByteSnapshot? byteSnapshot = null)
    {
        ValidateEnum(
            level,
            nameof(level));

        ValidateEnum(
            category,
            nameof(category));

        ValidateEnum(
            severity,
            nameof(severity));

        ValidateNullableEnum(
            direction,
            nameof(direction));

        ValidateNullableEnum(
            outcome,
            nameof(outcome));

        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException(
                "Event name must not be empty.",
                nameof(eventName));
        }

        if (endpointId is not null &&
            string.IsNullOrWhiteSpace(endpointId))
        {
            throw new ArgumentException(
                "Endpoint identity must not be empty when supplied.",
                nameof(endpointId));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "Duration must not be negative.");
        }

        if (byteSnapshot is not null &&
            (level != RuntimeDiagnosticLevel.Bytes ||
             category != RuntimeDiagnosticCategory.TransportBytes ||
             direction is null))
        {
            throw new ArgumentException(
                "A byte snapshot requires the Bytes level, TransportBytes "
                + "category, and a direction.",
                nameof(byteSnapshot));
        }

        Level = level;
        Category = category;
        EventName = eventName.Trim();
        Severity = severity;
        EndpointId = endpointId?.Trim();
        AttachmentGeneration = attachmentGeneration;
        Direction = direction;
        OperationId = operationId;
        Duration = duration;
        Outcome = outcome;
        Details = CopyDetails(
            details);
        ByteSnapshot =
            byteSnapshot;
    }

    public RuntimeDiagnosticLevel Level { get; }

    public RuntimeDiagnosticCategory Category { get; }

    public string EventName { get; }

    public RuntimeDiagnosticSeverity Severity { get; }

    public string? EndpointId { get; }

    public Guid? AttachmentGeneration { get; }

    public RuntimeDiagnosticDirection? Direction { get; }

    public Guid? OperationId { get; }

    public TimeSpan? Duration { get; }

    public RuntimeDiagnosticOutcome? Outcome { get; }

    public IReadOnlyDictionary<string, string> Details { get; }

    public RuntimeDiagnosticByteSnapshot? ByteSnapshot { get; }

    private static IReadOnlyDictionary<string, string> CopyDetails(
        IReadOnlyDictionary<string, string>? details)
    {
        if (details is null ||
            details.Count == 0)
        {
            return EmptyDetails;
        }

        Dictionary<string, string> copy =
            new(
                details.Count,
                StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> detail in details)
        {
            if (string.IsNullOrWhiteSpace(detail.Key))
            {
                throw new ArgumentException(
                    "Diagnostic detail keys must not be empty.",
                    nameof(details));
            }

            ArgumentNullException.ThrowIfNull(
                detail.Value,
                nameof(details));

            copy.Add(
                detail.Key.Trim(),
                detail.Value);
        }

        return new ReadOnlyDictionary<string, string>(
            copy);
    }

    private static void ValidateEnum<TEnum>(
        TEnum value,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(
                value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value is not defined.");
        }
    }

    private static void ValidateNullableEnum<TEnum>(
        TEnum? value,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (value is not null)
        {
            ValidateEnum(
                value.Value,
                parameterName);
        }
    }
}
