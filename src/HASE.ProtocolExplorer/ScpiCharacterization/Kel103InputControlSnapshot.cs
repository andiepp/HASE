namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103InputControlSnapshot(
    string Mode,
    string Voltage,
    string Current,
    string Resistance,
    string Power);
