namespace Hase.Mcnf.RfLab;

/// <summary>
/// The RF-Lab configuration reported by the standard read-configuration
/// handshake.
/// </summary>
public sealed record RfLabConfiguration(
    byte VariableSetCount,
    byte ActiveVariableSet,
    byte Capabilities,
    bool LedOn,
    bool Si5351Present);
