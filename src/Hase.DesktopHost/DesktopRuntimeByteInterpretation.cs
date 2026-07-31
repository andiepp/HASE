using System.Collections.ObjectModel;

namespace Hase.DesktopHost;

/// <summary>
/// Represents one immutable, read-only interpretation result.
/// </summary>
public sealed class DesktopRuntimeByteInterpretation
{
    private readonly ReadOnlyCollection<DesktopRuntimeByteField> fields;

    public DesktopRuntimeByteInterpretation(
        DesktopRuntimeByteInterpretationStatus status,
        string protocolFamily,
        string summary,
        IReadOnlyList<DesktopRuntimeByteField>? fields = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        ArgumentNullException.ThrowIfNull(
            protocolFamily);

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException(
                "Interpretation summary must not be empty.",
                nameof(summary));
        }

        Status = status;
        ProtocolFamily = protocolFamily.Trim();
        Summary = summary.Trim();

        this.fields =
            Array.AsReadOnly(
                fields?.ToArray()
                ?? []);
    }

    public DesktopRuntimeByteInterpretationStatus Status { get; }

    public string ProtocolFamily { get; }

    public string Summary { get; }

    public IReadOnlyList<DesktopRuntimeByteField> Fields => fields;

    public bool IsRecognized =>
        Status == DesktopRuntimeByteInterpretationStatus.RecognizedValid
        || Status == DesktopRuntimeByteInterpretationStatus
            .RecognizedMalformedOrIncomplete;
}
