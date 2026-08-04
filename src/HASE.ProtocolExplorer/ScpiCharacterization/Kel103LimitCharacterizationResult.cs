using Hase.Scpi.Kel103;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103LimitCharacterizationResult(
    Kel103Identity Identity,
    Kel103StateCandidate Candidate,
    Kel103SetpointLimit Limit,
    string? NormalizedValue,
    string UnitSymbol,
    Kel103UnrecognizedStateResponseObservation? UnrecognizedResponse,
    TimeSpan IdentityDuration,
    TimeSpan LimitDuration);
