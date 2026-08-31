namespace Hase.Mcnf.RfLab;

/// <summary>
/// Names the dedicated operating surface the RF-Lab instrument declares.
/// </summary>
/// <remarks>
/// The identifier is shared by the definition that declares it and by the
/// presentation layer that hosts it, so neither side carries a literal the
/// other could drift from.
/// </remarks>
public static class RfLabPanelDeclaration
{
    public const string PanelId = "rf-lab-signal-lab";
}
