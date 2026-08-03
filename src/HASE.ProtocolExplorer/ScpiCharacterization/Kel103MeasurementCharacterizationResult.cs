using Hase.Scpi.Kel103;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103MeasurementCharacterizationResult(
    Kel103Identity Identity,
    Kel103MeasurementCandidate Candidate,
    decimal Value,
    string UnitSymbol,
    TimeSpan IdentityDuration,
    TimeSpan MeasurementDuration);
