namespace Hase.Scpi.Kel103.Runtime;

public sealed record Kel103ModeSelectionMutationResult(
    Kel103OperatingMode OperatingMode,
    bool InputEnabled,
    DateTimeOffset TimestampUtc);
