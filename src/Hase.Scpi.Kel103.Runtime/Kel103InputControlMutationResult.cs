namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103InputControlMutationResult(
    bool InputEnabled,
    DateTimeOffset TimestampUtc);
