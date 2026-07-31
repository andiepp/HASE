using System.Buffers.Binary;
using System.Globalization;
using Hase.Protocol;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Safely interprets a bounded copy of one Native Protocol V1 envelope frame.
/// </summary>
public sealed class NativeProtocolV1DesktopRuntimeByteInterpreter
    : IDesktopRuntimeByteInterpreter
{
    public const string NativeProtocolFamily =
        "NativeProtocolV1";

    private const int HeaderLength =
        12;

    public string ProtocolFamily =>
        NativeProtocolFamily;

    public DesktopRuntimeByteInterpretation Interpret(
        RuntimeDiagnosticByteSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        byte[] bytes =
            snapshot.ToArray();

        var fields =
            new List<DesktopRuntimeByteField>();

        bool invalid =
            false;
        bool incomplete =
            false;

        AddVersionField(
            fields,
            bytes,
            offset: 0,
            "Protocol major version",
            ProtocolVersion.Current.Major,
            ref invalid,
            ref incomplete);

        AddVersionField(
            fields,
            bytes,
            offset: 1,
            "Protocol minor version",
            ProtocolVersion.Current.Minor,
            ref invalid,
            ref incomplete);

        AddEnumField<ProtocolMessageRole>(
            fields,
            bytes,
            offset: 2,
            "Message role",
            ref invalid,
            ref incomplete);

        AddEnumField<ProtocolMessageType>(
            fields,
            bytes,
            offset: 3,
            "Message type",
            ref invalid,
            ref incomplete);

        AddCorrelationField(
            fields,
            bytes,
            ref incomplete);

        uint? payloadLength =
            AddPayloadLengthField(
                fields,
                bytes,
                snapshot,
                ref invalid,
                ref incomplete);

        if (payloadLength is not null)
        {
            AddPayloadField(
                fields,
                bytes,
                snapshot,
                payloadLength.Value,
                ref invalid,
                ref incomplete);
        }

        DesktopRuntimeByteInterpretationStatus status =
            invalid || incomplete
                ? DesktopRuntimeByteInterpretationStatus
                    .RecognizedMalformedOrIncomplete
                : DesktopRuntimeByteInterpretationStatus
                    .RecognizedValid;

        return new DesktopRuntimeByteInterpretation(
            status,
            ProtocolFamily,
            CreateSummary(
                snapshot,
                invalid,
                incomplete),
            fields);
    }

    private static void AddVersionField(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        int offset,
        string name,
        byte expected,
        ref bool invalid,
        ref bool incomplete)
    {
        if (offset >= bytes.Length)
        {
            fields.Add(
                IncompleteField(
                    offset,
                    1,
                    name,
                    []));
            incomplete =
                true;
            return;
        }

        byte value =
            bytes[offset];
        bool isValid =
            value == expected;

        fields.Add(
            new DesktopRuntimeByteField(
                offset,
                1,
                name,
                isValid
                    ? $"{value} — current"
                    : $"{value} — expected {expected}",
                bytes.AsSpan(
                    offset,
                    1),
                isValid
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid));

        invalid |=
            !isValid;
    }

    private static void AddEnumField<TEnum>(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        int offset,
        string name,
        ref bool invalid,
        ref bool incomplete)
        where TEnum : struct, Enum
    {
        if (offset >= bytes.Length)
        {
            fields.Add(
                IncompleteField(
                    offset,
                    1,
                    name,
                    []));
            incomplete =
                true;
            return;
        }

        byte encoded =
            bytes[offset];
        TEnum value =
            (TEnum)Enum.ToObject(
                typeof(TEnum),
                encoded);
        bool isValid =
            Enum.IsDefined(
                value);

        fields.Add(
            new DesktopRuntimeByteField(
                offset,
                1,
                name,
                isValid
                    ? $"{value} ({encoded})"
                    : $"Unknown ({encoded})",
                bytes.AsSpan(
                    offset,
                    1),
                isValid
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid));

        invalid |=
            !isValid;
    }

    private static void AddCorrelationField(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        ref bool incomplete)
    {
        const int offset = 4;
        const int length = 4;
        int available =
            AvailableLength(
                bytes,
                offset,
                length);

        if (available < length)
        {
            fields.Add(
                IncompleteField(
                    offset,
                    length,
                    "Correlation ID",
                    CapturedSpan(
                        bytes,
                        offset,
                        available)));
            incomplete =
                true;
            return;
        }

        uint value =
            BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(
                    offset,
                    length));

        fields.Add(
            new DesktopRuntimeByteField(
                offset,
                length,
                "Correlation ID",
                value == 0
                    ? "0 — none"
                    : value.ToString(
                        CultureInfo.InvariantCulture),
                bytes.AsSpan(
                    offset,
                    length),
                DesktopRuntimeByteFieldValidation.Valid));
    }

    private static uint? AddPayloadLengthField(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        RuntimeDiagnosticByteSnapshot snapshot,
        ref bool invalid,
        ref bool incomplete)
    {
        const int offset = 8;
        const int length = 4;
        int available =
            AvailableLength(
                bytes,
                offset,
                length);

        if (available < length)
        {
            fields.Add(
                IncompleteField(
                    offset,
                    length,
                    "Payload length",
                    CapturedSpan(
                        bytes,
                        offset,
                        available)));
            incomplete =
                true;
            return null;
        }

        uint payloadLength =
            BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(
                    offset,
                    length));

        bool representable =
            payloadLength <= int.MaxValue;
        long expectedFrameLength =
            HeaderLength
            + (long)payloadLength;
        bool matchesOriginal =
            representable
            && expectedFrameLength
                == snapshot.OriginalByteCount;

        DesktopRuntimeByteFieldValidation validation =
            matchesOriginal
                ? DesktopRuntimeByteFieldValidation.Valid
                : DesktopRuntimeByteFieldValidation.Invalid;

        string interpretation =
            payloadLength.ToString(
                CultureInfo.InvariantCulture)
            + " bytes";

        if (!representable)
        {
            interpretation +=
                " — exceeds supported length";
        }
        else if (!matchesOriginal)
        {
            interpretation +=
                expectedFrameLength < snapshot.OriginalByteCount
                    ? " — trailing frame bytes"
                    : " — exceeds available frame bytes";
        }

        fields.Add(
            new DesktopRuntimeByteField(
                offset,
                length,
                "Payload length",
                interpretation,
                bytes.AsSpan(
                    offset,
                    length),
                validation));

        invalid |=
            validation
            == DesktopRuntimeByteFieldValidation.Invalid;

        return payloadLength;
    }

    private static void AddPayloadField(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        RuntimeDiagnosticByteSnapshot snapshot,
        uint payloadLength,
        ref bool invalid,
        ref bool incomplete)
    {
        int declaredLength =
            payloadLength > int.MaxValue
                ? int.MaxValue
                : (int)payloadLength;
        int capturedLength =
            AvailableLength(
                bytes,
                HeaderLength,
                declaredLength);
        long expectedFrameLength =
            HeaderLength
            + (long)payloadLength;
        bool originalLengthMatches =
            expectedFrameLength
            == snapshot.OriginalByteCount;

        DesktopRuntimeByteFieldValidation validation;
        string interpretation;

        if (!originalLengthMatches)
        {
            validation =
                DesktopRuntimeByteFieldValidation.Invalid;
            interpretation =
                "Payload boundary is inconsistent with the original frame length.";
            invalid =
                true;
        }
        else if (capturedLength < declaredLength)
        {
            validation =
                DesktopRuntimeByteFieldValidation.Incomplete;
            interpretation =
                $"Payload body — {capturedLength}/{declaredLength} bytes captured";
            incomplete =
                true;
        }
        else
        {
            validation =
                DesktopRuntimeByteFieldValidation.Valid;
            interpretation =
                $"Payload body — {declaredLength} bytes";
        }

        fields.Add(
            new DesktopRuntimeByteField(
                HeaderLength,
                declaredLength,
                "Payload",
                interpretation,
                bytes.AsSpan(
                    HeaderLength,
                    capturedLength),
                validation));
    }

    private static DesktopRuntimeByteField IncompleteField(
        int offset,
        int length,
        string name,
        ReadOnlySpan<byte> bytes)
    {
        return new DesktopRuntimeByteField(
            offset,
            length,
            name,
            $"Incomplete — {bytes.Length}/{length} bytes captured",
            bytes,
            DesktopRuntimeByteFieldValidation.Incomplete);
    }

    private static int AvailableLength(
        byte[] bytes,
        int offset,
        int requestedLength)
    {
        if (offset >= bytes.Length)
        {
            return 0;
        }

        return Math.Min(
            requestedLength,
            bytes.Length - offset);
    }

    private static ReadOnlySpan<byte> CapturedSpan(
        byte[] bytes,
        int offset,
        int length)
    {
        return offset >= bytes.Length
            ? ReadOnlySpan<byte>.Empty
            : bytes.AsSpan(
                offset,
                length);
    }

    private static string CreateSummary(
        RuntimeDiagnosticByteSnapshot snapshot,
        bool invalid,
        bool incomplete)
    {
        if (invalid)
        {
            return "Native Protocol V1 frame is structurally invalid.";
        }

        if (incomplete)
        {
            return snapshot.IsTruncated
                ? "Native Protocol V1 frame is structurally consistent but the diagnostic snapshot is truncated."
                : "Native Protocol V1 frame is incomplete.";
        }

        return "Native Protocol V1 frame is structurally valid.";
    }
}
