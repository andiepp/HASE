namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103ModeSelectionSnapshot(
    string Voltage,
    string Current,
    string Resistance,
    string Power);
