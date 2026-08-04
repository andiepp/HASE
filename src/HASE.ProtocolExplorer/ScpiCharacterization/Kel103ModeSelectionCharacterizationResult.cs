using Hase.Scpi.Kel103;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103ModeSelectionCharacterizationResult(
    Kel103Identity Identity,
    Kel103ModeSelection RequestedMode,
    TimeSpan IdentityDuration,
    TimeSpan DestinationDuration,
    TimeSpan RestorationDuration);
