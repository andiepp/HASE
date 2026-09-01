using Hase.Scpi.Kel103;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103SetpointChangeCharacterizationResult(
    Kel103Identity Identity,
    Kel103StateCandidate Candidate,
    TimeSpan IdentityDuration,
    TimeSpan ChangedValueVerificationDuration,
    TimeSpan SetpointRestorationDuration,
    bool ModeRestorationCommandTransmitted,
    TimeSpan ModeRestorationDuration);
