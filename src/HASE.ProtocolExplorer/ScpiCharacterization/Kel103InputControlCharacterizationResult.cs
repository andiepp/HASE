using Hase.Scpi.Kel103;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103InputControlCharacterizationResult(
    Kel103Identity Identity,
    TimeSpan IdentityDuration,
    TimeSpan ActivationDuration,
    TimeSpan DeactivationDuration);
