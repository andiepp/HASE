using System.Text;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Safely interprets one bounded SCPI text request or response snapshot.
/// </summary>
public sealed class ScpiTextDesktopRuntimeByteInterpreter
    : IDesktopRuntimeByteInterpreter
{
    public const string ScpiProtocolFamily = "ScpiText";

    public string ProtocolFamily => ScpiProtocolFamily;

    public DesktopRuntimeByteInterpretation Interpret(
        RuntimeDiagnosticByteSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        byte[] bytes = snapshot.ToArray();
        var fields = new List<DesktopRuntimeByteField>();
        int terminatorOffset = FindTerminator(bytes);
        bool incomplete = snapshot.IsTruncated;
        bool invalid = false;

        if (terminatorOffset < 0)
        {
            AddUnterminatedFields(
                fields,
                bytes,
                snapshot,
                ref invalid,
                ref incomplete);
        }
        else
        {
            AddTerminatedFields(
                fields,
                bytes,
                terminatorOffset,
                snapshot,
                ref invalid,
                ref incomplete);
        }

        return new DesktopRuntimeByteInterpretation(
            invalid || incomplete
                ? DesktopRuntimeByteInterpretationStatus
                    .RecognizedMalformedOrIncomplete
                : DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            ProtocolFamily,
            CreateSummary(
                bytes,
                terminatorOffset,
                snapshot,
                invalid,
                incomplete),
            fields);
    }

    private static void AddUnterminatedFields(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        RuntimeDiagnosticByteSnapshot snapshot,
        ref bool invalid,
        ref bool incomplete)
    {
        bool printable = IsPrintableAscii(bytes);
        DesktopRuntimeByteFieldValidation bodyValidation =
            printable
                ? DesktopRuntimeByteFieldValidation.Valid
                : DesktopRuntimeByteFieldValidation.Invalid;

        fields.Add(new DesktopRuntimeByteField(
            0,
            bytes.Length,
            "Message body",
            DescribeBody(bytes, printable),
            bytes,
            bodyValidation));
        fields.Add(new DesktopRuntimeByteField(
            0,
            bytes.Length,
            "Message classification",
            "Undetermined — a SCPI terminator is unavailable",
            bytes,
            snapshot.IsTruncated
                ? DesktopRuntimeByteFieldValidation.Incomplete
                : DesktopRuntimeByteFieldValidation.Invalid));
        fields.Add(new DesktopRuntimeByteField(
            bytes.Length,
            1,
            "Terminator",
            snapshot.IsTruncated
                ? "Not captured — the byte snapshot is truncated"
                : "Missing — expected CR for a request or LF for a response",
            [],
            snapshot.IsTruncated
                ? DesktopRuntimeByteFieldValidation.Incomplete
                : DesktopRuntimeByteFieldValidation.Invalid));

        invalid |= !printable || !snapshot.IsTruncated;
        incomplete |= snapshot.IsTruncated;
    }

    private static void AddTerminatedFields(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        int terminatorOffset,
        RuntimeDiagnosticByteSnapshot snapshot,
        ref bool invalid,
        ref bool incomplete)
    {
        ReadOnlySpan<byte> body = bytes.AsSpan(0, terminatorOffset);
        byte terminator = bytes[terminatorOffset];
        bool printable = IsPrintableAscii(body);
        bool bodyPresent = body.Length > 0;
        bool hasTrailingBytes = terminatorOffset < bytes.Length - 1;
        bool request = terminator == 0x0D;

        DesktopRuntimeByteFieldValidation bodyValidation =
            printable && bodyPresent
                ? DesktopRuntimeByteFieldValidation.Valid
                : DesktopRuntimeByteFieldValidation.Invalid;
        fields.Add(new DesktopRuntimeByteField(
            0,
            body.Length,
            "Message body",
            bodyPresent
                ? DescribeBody(body, printable)
                : "Empty — a SCPI message body is required",
            body,
            bodyValidation));

        string classification = request
            ? body.EndsWith("?"u8)
                ? "Query request"
                : "Command request"
            : "Response";
        fields.Add(new DesktopRuntimeByteField(
            0,
            body.Length,
            "Message classification",
            classification,
            body,
            bodyPresent && printable
                ? DesktopRuntimeByteFieldValidation.Valid
                : DesktopRuntimeByteFieldValidation.Invalid));

        fields.Add(new DesktopRuntimeByteField(
            terminatorOffset,
            1,
            "Terminator",
            request
                ? "CR (0D) — SCPI request terminator"
                : "LF (0A) — SCPI response terminator",
            bytes.AsSpan(terminatorOffset, 1),
            DesktopRuntimeByteFieldValidation.Valid));

        if (hasTrailingBytes)
        {
            ReadOnlySpan<byte> trailing = bytes.AsSpan(terminatorOffset + 1);
            fields.Add(new DesktopRuntimeByteField(
                terminatorOffset + 1,
                trailing.Length,
                "Trailing bytes",
                $"Invalid — {trailing.Length} byte(s) follow the SCPI terminator",
                trailing,
                DesktopRuntimeByteFieldValidation.Invalid));
        }

        invalid |= !printable || !bodyPresent || hasTrailingBytes;
        incomplete |= snapshot.IsTruncated;
    }

    private static int FindTerminator(ReadOnlySpan<byte> bytes)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] is 0x0D or 0x0A)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsPrintableAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value is < 0x20 or > 0x7E)
            {
                return false;
            }
        }

        return true;
    }

    private static string DescribeBody(
        ReadOnlySpan<byte> body,
        bool printable)
    {
        return printable
            ? $"{Encoding.ASCII.GetString(body)} — {body.Length} ASCII character(s)"
            : $"Contains unsupported non-printable or non-ASCII bytes — {body.Length} byte(s)";
    }

    private static string CreateSummary(
        byte[] bytes,
        int terminatorOffset,
        RuntimeDiagnosticByteSnapshot snapshot,
        bool invalid,
        bool incomplete)
    {
        if (incomplete)
        {
            return $"Incomplete SCPI text snapshot — captured {snapshot.CapturedByteCount} "
                + $"of {snapshot.OriginalByteCount} bytes.";
        }

        if (invalid)
        {
            return "Malformed SCPI text snapshot — inspect the structured fields.";
        }

        bool request = bytes[terminatorOffset] == 0x0D;
        string classification = request
            ? bytes.AsSpan(0, terminatorOffset).EndsWith("?"u8)
                ? "query request"
                : "command request"
            : "response";
        string terminator = request ? "CR" : "LF";
        return $"Valid SCPI {classification} — {terminator} terminator.";
    }
}
