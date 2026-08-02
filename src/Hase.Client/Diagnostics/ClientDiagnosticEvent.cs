using System.Collections.ObjectModel;

namespace Hase.Client.Diagnostics;

/// <summary>
/// Describes one structured client diagnostic event before local sequencing.
/// </summary>
public sealed class ClientDiagnosticEvent
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>());

    public ClientDiagnosticEvent(
        ClientDiagnosticLevel level,
        ClientDiagnosticCategory category,
        string eventName,
        ClientDiagnosticSeverity severity =
            ClientDiagnosticSeverity.Information,
        ClientDiagnosticDirection? direction = null,
        Guid? operationId = null,
        string? endpointId = null,
        Guid? attachmentGeneration = null,
        string? instrumentId = null,
        string? descriptorPath = null,
        TimeSpan? duration = null,
        ClientDiagnosticOutcome? outcome = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        ClientDiagnosticSessionContext? sessionContext = null)
    {
        ValidateEnum(level, nameof(level));
        ValidateEnum(category, nameof(category));
        ValidateEnum(severity, nameof(severity));
        ValidateNullableEnum(direction, nameof(direction));
        ValidateNullableEnum(outcome, nameof(outcome));

        EventName = RequireText(eventName, nameof(eventName));
        EndpointId = OptionalText(endpointId, nameof(endpointId));
        InstrumentId = OptionalText(instrumentId, nameof(instrumentId));
        DescriptorPath = OptionalText(descriptorPath, nameof(descriptorPath));

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "Duration must not be negative.");
        }

        Level = level;
        Category = category;
        Severity = severity;
        Direction = direction;
        OperationId = operationId;
        AttachmentGeneration = attachmentGeneration;
        Duration = duration;
        Outcome = outcome;
        Metadata = CopyMetadata(metadata);
        SessionContext = sessionContext;
    }

    public ClientDiagnosticLevel Level { get; }
    public ClientDiagnosticCategory Category { get; }
    public string EventName { get; }
    public ClientDiagnosticSeverity Severity { get; }
    public ClientDiagnosticDirection? Direction { get; }
    public Guid? OperationId { get; }
    public string? EndpointId { get; }
    public Guid? AttachmentGeneration { get; }
    public string? InstrumentId { get; }
    public string? DescriptorPath { get; }
    public TimeSpan? Duration { get; }
    public ClientDiagnosticOutcome? Outcome { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public ClientDiagnosticSessionContext? SessionContext { get; }

    private static IReadOnlyDictionary<string, string> CopyMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> copy =
            new(metadata.Count, StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in metadata)
        {
            string key = RequireText(item.Key, nameof(metadata));
            ArgumentNullException.ThrowIfNull(item.Value, nameof(metadata));

            string normalizedKey =
                new string(
                    key.Where(char.IsLetterOrDigit).ToArray())
                    .ToLowerInvariant();

            if (IsProhibitedMetadataKey(normalizedKey))
            {
                throw new ArgumentException(
                    "Diagnostic metadata must not contain secrets, credentials, "
                    + "or network-location fields.",
                    nameof(metadata));
            }

            copy.Add(key, item.Value);
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }

    private static bool IsProhibitedMetadataKey(string normalizedKey)
    {
        return normalizedKey.Contains("password", StringComparison.Ordinal) ||
               normalizedKey.Contains("privatekey", StringComparison.Ordinal) ||
               normalizedKey.Contains("credential", StringComparison.Ordinal) ||
               normalizedKey.Contains("secret", StringComparison.Ordinal) ||
               normalizedKey.EndsWith("token", StringComparison.Ordinal) ||
               normalizedKey.EndsWith("address", StringComparison.Ordinal) ||
               normalizedKey.EndsWith("hostname", StringComparison.Ordinal) ||
               normalizedKey.EndsWith("uri", StringComparison.Ordinal) ||
               normalizedKey.EndsWith("url", StringComparison.Ordinal);
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }

    private static string? OptionalText(string? value, string parameterName)
    {
        return value is null ? null : RequireText(value, parameterName);
    }

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
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
            ValidateEnum(value.Value, parameterName);
        }
    }
}
