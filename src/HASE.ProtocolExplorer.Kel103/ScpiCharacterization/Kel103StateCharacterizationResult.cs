using Hase.Scpi.Kel103;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103StateCharacterizationResult(
    Kel103Identity Identity,
    Kel103StateCandidate Candidate,
    string? NormalizedValue,
    string? UnitSymbol,
    Kel103UnrecognizedStateResponseObservation? UnrecognizedResponse,
    TimeSpan IdentityDuration,
    TimeSpan StateDuration);
