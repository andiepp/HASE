using System.Buffers.Binary;
using System.Globalization;
using Hase.CompactProtocol;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Safely interprets a bounded copy of one Compact Serial Protocol V1 frame.
/// </summary>
public sealed class CompactSerialProtocolV1DesktopRuntimeByteInterpreter
    : IDesktopRuntimeByteInterpreter
{
    public const string CompactProtocolFamily =
        "CompactSerialProtocolV1";

    public string ProtocolFamily =>
        CompactProtocolFamily;

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

        AddStartMarker(
            fields,
            bytes,
            ref invalid,
            ref incomplete);
        AddVersion(
            fields,
            bytes,
            ref invalid,
            ref incomplete);

        byte? messageType =
            AddMessageType(
                fields,
                bytes,
                ref invalid,
                ref incomplete);

        AddCorrelationId(
            fields,
            bytes,
            messageType,
            ref invalid,
            ref incomplete);

        byte? payloadLength =
            AddPayloadLength(
                fields,
                bytes,
                snapshot,
                ref invalid,
                ref incomplete);

        if (payloadLength is not null)
        {
            AddPayloadAndCrc(
                fields,
                bytes,
                snapshot,
                payloadLength.Value,
                ref invalid,
                ref incomplete);
        }

        return new DesktopRuntimeByteInterpretation(
            invalid || incomplete
                ? DesktopRuntimeByteInterpretationStatus
                    .RecognizedMalformedOrIncomplete
                : DesktopRuntimeByteInterpretationStatus
                    .RecognizedValid,
            ProtocolFamily,
            CreateSummary(
                snapshot,
                invalid,
                incomplete),
            fields);
    }

    private static void AddStartMarker(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        ref bool invalid,
        ref bool incomplete)
    {
        int available =
            AvailableLength(
                bytes,
                0,
                2);

        if (available < 2)
        {
            fields.Add(
                IncompleteField(
                    0,
                    2,
                    "Start marker",
                    CapturedSpan(
                        bytes,
                        0,
                        available)));
            incomplete =
                true;
            return;
        }

        bool valid =
            bytes[0]
                == CompactSerialProtocolV1Inspection.StartMarkerFirstByte
            && bytes[1]
                == CompactSerialProtocolV1Inspection.StartMarkerSecondByte;

        fields.Add(
            new DesktopRuntimeByteField(
                0,
                2,
                "Start marker",
                valid
                    ? "HS — valid Compact frame marker"
                    : "Invalid — expected 48 53",
                bytes.AsSpan(
                    0,
                    2),
                valid
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid));

        invalid |=
            !valid;
    }

    private static void AddVersion(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        ref bool invalid,
        ref bool incomplete)
    {
        if (bytes.Length <= 2)
        {
            fields.Add(
                IncompleteField(
                    2,
                    1,
                    "Protocol version",
                    []));
            incomplete =
                true;
            return;
        }

        byte value =
            bytes[2];
        bool valid =
            value
            == CompactSerialProtocolV1Inspection.ProtocolVersion;

        fields.Add(
            new DesktopRuntimeByteField(
                2,
                1,
                "Protocol version",
                valid
                    ? "1 — current"
                    : $"{value} — expected 1",
                bytes.AsSpan(
                    2,
                    1),
                valid
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid));

        invalid |=
            !valid;
    }

    private static byte? AddMessageType(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        ref bool invalid,
        ref bool incomplete)
    {
        if (bytes.Length <= 3)
        {
            fields.Add(
                IncompleteField(
                    3,
                    1,
                    "Message type",
                    []));
            incomplete =
                true;
            return null;
        }

        byte value =
            bytes[3];
        bool valid =
            CompactSerialProtocolV1Inspection.TryGetMessageTypeName(
                value,
                out string name);

        fields.Add(
            new DesktopRuntimeByteField(
                3,
                1,
                "Message type",
                valid
                    ? $"{name} (0x{value:X2})"
                    : $"Unknown (0x{value:X2})",
                bytes.AsSpan(
                    3,
                    1),
                valid
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid));

        invalid |=
            !valid;
        return value;
    }

    private static void AddCorrelationId(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        byte? messageType,
        ref bool invalid,
        ref bool incomplete)
    {
        if (bytes.Length <= 4)
        {
            fields.Add(
                IncompleteField(
                    4,
                    1,
                    "Correlation ID",
                    []));
            incomplete =
                true;
            return;
        }

        byte value =
            bytes[4];
        DesktopRuntimeByteFieldValidation validation =
            DesktopRuntimeByteFieldValidation.NotApplicable;
        string interpretation =
            value == 0
                ? "0 — unsolicited notification"
                : value.ToString(
                    CultureInfo.InvariantCulture);

        if (messageType is not null
            && CompactSerialProtocolV1Inspection
                .RequiresZeroCorrelationId(
                    messageType.Value))
        {
            validation =
                value == 0
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid;
        }
        else if (messageType is not null
            && CompactSerialProtocolV1Inspection
                .RequiresNonZeroCorrelationId(
                    messageType.Value))
        {
            validation =
                value != 0
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid;
        }

        if (validation
            == DesktopRuntimeByteFieldValidation.Invalid)
        {
            interpretation +=
                value == 0
                    ? " — request/response requires nonzero"
                    : " — notification requires zero";
            invalid =
                true;
        }

        fields.Add(
            new DesktopRuntimeByteField(
                4,
                1,
                "Correlation ID",
                interpretation,
                bytes.AsSpan(
                    4,
                    1),
                validation));
    }

    private static byte? AddPayloadLength(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        RuntimeDiagnosticByteSnapshot snapshot,
        ref bool invalid,
        ref bool incomplete)
    {
        if (bytes.Length <= 5)
        {
            fields.Add(
                IncompleteField(
                    5,
                    1,
                    "Payload length",
                    []));
            incomplete =
                true;
            return null;
        }

        byte value =
            bytes[5];
        int expectedFrameLength =
            CompactSerialProtocolV1Inspection.FrameOverheadLength
            + value;
        bool valid =
            expectedFrameLength
            == snapshot.OriginalByteCount;
        string interpretation =
            $"{value} bytes";

        if (!valid)
        {
            interpretation +=
                expectedFrameLength < snapshot.OriginalByteCount
                    ? " — trailing frame bytes"
                    : " — exceeds available frame bytes";
            invalid =
                true;
        }

        fields.Add(
            new DesktopRuntimeByteField(
                5,
                1,
                "Payload length",
                interpretation,
                bytes.AsSpan(
                    5,
                    1),
                valid
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid));

        return value;
    }

    private static void AddPayloadAndCrc(
        ICollection<DesktopRuntimeByteField> fields,
        byte[] bytes,
        RuntimeDiagnosticByteSnapshot snapshot,
        int payloadLength,
        ref bool invalid,
        ref bool incomplete)
    {
        const int payloadOffset = 6;
        int expectedFrameLength =
            CompactSerialProtocolV1Inspection.FrameOverheadLength
            + payloadLength;
        bool frameLengthValid =
            expectedFrameLength
            == snapshot.OriginalByteCount;
        int capturedPayloadLength =
            AvailableLength(
                bytes,
                payloadOffset,
                payloadLength);

        DesktopRuntimeByteFieldValidation payloadValidation =
            !frameLengthValid
                ? DesktopRuntimeByteFieldValidation.Invalid
                : capturedPayloadLength < payloadLength
                    ? DesktopRuntimeByteFieldValidation.Incomplete
                    : DesktopRuntimeByteFieldValidation.Valid;

        if (payloadValidation
            == DesktopRuntimeByteFieldValidation.Invalid)
        {
            invalid =
                true;
        }
        else if (payloadValidation
            == DesktopRuntimeByteFieldValidation.Incomplete)
        {
            incomplete =
                true;
        }

        fields.Add(
            new DesktopRuntimeByteField(
                payloadOffset,
                payloadLength,
                "Payload",
                payloadValidation
                    == DesktopRuntimeByteFieldValidation.Valid
                        ? $"Payload body — {payloadLength} bytes"
                        : payloadValidation
                            == DesktopRuntimeByteFieldValidation.Incomplete
                            ? $"Payload body — {capturedPayloadLength}/{payloadLength} bytes captured"
                            : "Payload boundary is inconsistent with the original frame length.",
                CapturedSpan(
                    bytes,
                    payloadOffset,
                    capturedPayloadLength),
                payloadValidation));

        int crcOffset =
            payloadOffset
            + payloadLength;
        int capturedCrcLength =
            AvailableLength(
                bytes,
                crcOffset,
                2);

        if (capturedPayloadLength < payloadLength
            || capturedCrcLength < 2)
        {
            fields.Add(
                IncompleteField(
                    crcOffset,
                    2,
                    "CRC-16/CCITT-FALSE",
                    CapturedSpan(
                        bytes,
                        crcOffset,
                        capturedCrcLength)));
            incomplete =
                true;
            return;
        }

        ushort transmitted =
            BinaryPrimitives.ReadUInt16BigEndian(
                bytes.AsSpan(
                    crcOffset,
                    2));
        ushort calculated =
            CompactSerialProtocolV1Inspection.CalculateCrc(
                bytes.AsSpan(
                    2,
                    4 + payloadLength));
        bool validCrc =
            transmitted == calculated;

        fields.Add(
            new DesktopRuntimeByteField(
                crcOffset,
                2,
                "CRC-16/CCITT-FALSE",
                $"Transmitted 0x{transmitted:X4}; calculated 0x{calculated:X4} — "
                    + (validCrc ? "valid" : "invalid"),
                bytes.AsSpan(
                    crcOffset,
                    2),
                validCrc
                    ? DesktopRuntimeByteFieldValidation.Valid
                    : DesktopRuntimeByteFieldValidation.Invalid));

        invalid |=
            !validCrc;
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
        return offset >= bytes.Length
            ? 0
            : Math.Min(
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
            return "Compact Serial Protocol V1 frame is structurally invalid.";
        }

        if (incomplete)
        {
            return snapshot.IsTruncated
                ? "Compact Serial Protocol V1 frame is structurally consistent but the diagnostic snapshot is truncated."
                : "Compact Serial Protocol V1 frame is incomplete.";
        }

        return "Compact Serial Protocol V1 frame is structurally valid.";
    }
}
