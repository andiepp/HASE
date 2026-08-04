using Hase.Scpi.Kel103;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103SetpointWriteCharacterizationResult(
    Kel103Identity Identity,
    Kel103StateCandidate Candidate,
    TimeSpan IdentityDuration,
    TimeSpan SetterVerificationDuration,
    bool RestorationCommandTransmitted,
    TimeSpan RestorationVerificationDuration);
