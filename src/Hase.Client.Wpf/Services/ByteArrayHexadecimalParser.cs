using Hase.Core.Domain.Data;

namespace Hase.Client.Wpf.Services;

/// <summary>
/// Compatibility facade for callers compiled against the ADR-0036 WPF
/// service. All parsing semantics are owned by Hase.Operator.Input.
/// </summary>
public static class ByteArrayHexadecimalParser
{
    public static bool TryParse(
        string? text,
        out ByteArrayValue? value)
    {
        return Hase.Operator.Input.ByteArrayHexadecimalParser.TryParse(
            text,
            out value);
    }
}
