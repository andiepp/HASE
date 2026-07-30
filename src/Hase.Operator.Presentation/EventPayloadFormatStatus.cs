namespace Hase.Operator.Presentation;

/// <summary>
/// Describes the outcome of formatting one Event payload.
/// </summary>
public enum EventPayloadFormatStatus
{
    NoPayload = 0,
    Formatted = 1,
    MissingPayload = 2,
    UnexpectedPayload = 3,
    TypeMismatch = 4,
    UnsupportedDescriptor = 5
}
