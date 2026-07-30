namespace Hase.Operator.Presentation;

/// <summary>
/// Contains one non-throwing Event payload presentation result.
/// </summary>
public sealed record EventPayloadFormatResult
{
    public EventPayloadFormatResult(
        EventPayloadFormatStatus status,
        string text)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        Status = status;
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public EventPayloadFormatStatus Status { get; }

    public string Text { get; }
}
