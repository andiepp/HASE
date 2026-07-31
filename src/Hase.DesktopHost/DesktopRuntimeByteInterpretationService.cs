using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Safely dispatches copied diagnostic snapshots to read-only protocol-family
/// interpreters.
/// </summary>
public sealed class DesktopRuntimeByteInterpretationService
{
    private readonly IReadOnlyDictionary<string, IDesktopRuntimeByteInterpreter>
        interpreters;

    public DesktopRuntimeByteInterpretationService(
        IEnumerable<IDesktopRuntimeByteInterpreter>? interpreters = null)
    {
        var byProtocolFamily =
            new Dictionary<string, IDesktopRuntimeByteInterpreter>(
                StringComparer.Ordinal);

        foreach (IDesktopRuntimeByteInterpreter interpreter
            in interpreters ?? [])
        {
            ArgumentNullException.ThrowIfNull(
                interpreter,
                nameof(interpreters));

            if (string.IsNullOrWhiteSpace(
                    interpreter.ProtocolFamily))
            {
                throw new ArgumentException(
                    "Interpreter protocol family must not be empty.",
                    nameof(interpreters));
            }

            if (!byProtocolFamily.TryAdd(
                    interpreter.ProtocolFamily.Trim(),
                    interpreter))
            {
                throw new ArgumentException(
                    "Only one interpreter may be registered for each protocol family.",
                    nameof(interpreters));
            }
        }

        this.interpreters =
            byProtocolFamily;
    }

    public static DesktopRuntimeByteInterpretationService CreateDefault()
    {
        return new DesktopRuntimeByteInterpretationService(
            [
                new NativeProtocolV1DesktopRuntimeByteInterpreter(),
                new CompactSerialProtocolV1DesktopRuntimeByteInterpreter()
            ]);
    }

    public DesktopRuntimeByteInterpretation Interpret(
        string? protocolFamily,
        RuntimeDiagnosticByteSnapshot? snapshot)
    {
        string normalizedProtocolFamily =
            protocolFamily?.Trim()
            ?? string.Empty;

        if (snapshot is null
            || snapshot.CapturedByteCount == 0)
        {
            return new DesktopRuntimeByteInterpretation(
                DesktopRuntimeByteInterpretationStatus.NoCapturedBytes,
                normalizedProtocolFamily,
                "No captured bytes are available for interpretation.");
        }

        if (!interpreters.TryGetValue(
                normalizedProtocolFamily,
                out IDesktopRuntimeByteInterpreter? interpreter))
        {
            return new DesktopRuntimeByteInterpretation(
                DesktopRuntimeByteInterpretationStatus.UnsupportedProtocolFamily,
                normalizedProtocolFamily,
                string.IsNullOrEmpty(normalizedProtocolFamily)
                    ? "The diagnostic record does not identify a protocol family."
                    : $"No byte interpreter is registered for '{normalizedProtocolFamily}'.");
        }

        try
        {
            return interpreter.Interpret(
                    snapshot)
                ?? new DesktopRuntimeByteInterpretation(
                    DesktopRuntimeByteInterpretationStatus
                        .RecognizedMalformedOrIncomplete,
                    normalizedProtocolFamily,
                    "The byte interpreter returned no result.");
        }
        catch
        {
            return new DesktopRuntimeByteInterpretation(
                DesktopRuntimeByteInterpretationStatus
                    .RecognizedMalformedOrIncomplete,
                normalizedProtocolFamily,
                "The captured bytes could not be interpreted safely.");
        }
    }
}
