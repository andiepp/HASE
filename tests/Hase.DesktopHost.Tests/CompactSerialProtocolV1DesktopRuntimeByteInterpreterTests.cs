using System.Buffers.Binary;
using Hase.CompactProtocol;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class CompactSerialProtocolV1DesktopRuntimeByteInterpreterTests
{
    private readonly CompactSerialProtocolV1DesktopRuntimeByteInterpreter interpreter =
        new();

    [Fact]
    public void ProtocolFamily_ShouldMatchDiagnosticDiscriminator()
    {
        Assert.Equal("CompactSerialProtocolV1", interpreter.ProtocolFamily);
    }

    [Fact]
    public void Interpret_ValidFrame_ShouldProjectExactFieldsAndCrc()
    {
        byte[] frame = CreateFrame(0x05, 3, [0x01, 0x02]);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
        Assert.Equal(7, result.Fields.Count);
        AssertField(result, 0, 2, "Start marker", "HS — valid Compact frame marker", "48 53");
        AssertField(result, 2, 1, "Protocol version", "1 — current", "01");
        AssertField(result, 3, 1, "Message type", "ReadPropertyRequest (0x05)", "05");
        AssertField(result, 4, 1, "Correlation ID", "3", "03");
        AssertField(result, 5, 1, "Payload length", "2 bytes", "02");
        AssertField(result, 6, 2, "Payload", "Payload body — 2 bytes", "01 02");
        Assert.Contains(
            "— valid",
            result.Fields.Single(field => field.Name == "CRC-16/CCITT-FALSE").InterpretedValue);
    }

    [Fact]
    public void Interpret_EventNotificationWithZeroCorrelation_ShouldBeValid()
    {
        byte[] frame = CreateFrame(0x09, 0, [0x01]);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        DesktopRuntimeByteField correlation =
            result.Fields.Single(field => field.Name == "Correlation ID");
        Assert.Equal("0 — unsolicited notification", correlation.InterpretedValue);
        Assert.Equal(DesktopRuntimeByteFieldValidation.Valid, correlation.Validation);
    }

    [Fact]
    public void Interpret_RequestWithZeroCorrelation_ShouldBeInvalid()
    {
        byte[] frame = CreateFrame(0x05, 0, []);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Correlation ID").Validation);
    }

    [Fact]
    public void Interpret_NotificationWithNonzeroCorrelation_ShouldBeInvalid()
    {
        byte[] frame = CreateFrame(0x09, 1, [0x01]);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Correlation ID").Validation);
    }

    [Fact]
    public void Interpret_InvalidMarker_ShouldMarkMarkerInvalid()
    {
        byte[] frame = CreateFrame(0x01, 1, []);
        frame[0] = 0x00;

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Start marker").Validation);
    }

    [Fact]
    public void Interpret_InvalidVersion_ShouldMarkVersionInvalid()
    {
        byte[] frame = CreateFrame(0x01, 1, []);
        frame[2] = 0x02;
        RewriteCrc(frame);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Protocol version").Validation);
    }

    [Fact]
    public void Interpret_UnknownMessageType_ShouldMarkTypeInvalid()
    {
        byte[] frame = CreateFrame(0xFF, 1, []);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Message type").Validation);
    }

    [Fact]
    public void Interpret_CorruptedCrc_ShouldReportTransmittedAndCalculated()
    {
        byte[] frame = CreateFrame(0x05, 1, [0xAA]);
        frame[^1] ^= 0xFF;

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        DesktopRuntimeByteField crc =
            result.Fields.Single(field => field.Name == "CRC-16/CCITT-FALSE");
        Assert.Equal(DesktopRuntimeByteFieldValidation.Invalid, crc.Validation);
        Assert.Contains("Transmitted", crc.InterpretedValue);
        Assert.Contains("calculated", crc.InterpretedValue);
    }

    [Fact]
    public void Interpret_DeclaredLengthMismatch_ShouldMarkBoundaryInvalid()
    {
        byte[] frame = CreateFrame(0x05, 1, []);
        frame[5] = 3;

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Payload length").Validation);
    }

    [Fact]
    public void Interpret_IncompleteHeader_ShouldPreservePartialFields()
    {
        byte[] frame = [0x48, 0x53, 0x01, 0x05];

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Incomplete,
            result.Fields.Single(field => field.Name == "Correlation ID").Validation);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Incomplete,
            result.Fields.Single(field => field.Name == "Payload length").Validation);
    }

    [Fact]
    public void Interpret_MaximumFrameTruncatedByDiagnosticBound_ShouldBeIncomplete()
    {
        byte[] payload = Enumerable.Range(0, 255).Select(index => (byte)index).ToArray();
        byte[] complete = CreateFrame(0x05, 1, payload);
        byte[] captured = complete[..RuntimeDiagnosticByteSnapshot.MaximumCapturedByteCount];
        var snapshot = new RuntimeDiagnosticByteSnapshot(
            complete.Length,
            captured,
            isTruncated: true);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(snapshot);

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedMalformedOrIncomplete,
            result.Status);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Valid,
            result.Fields.Single(field => field.Name == "Payload length").Validation);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Incomplete,
            result.Fields.Single(field => field.Name == "Payload").Validation);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Incomplete,
            result.Fields.Single(field => field.Name == "CRC-16/CCITT-FALSE").Validation);
        Assert.Contains("structurally consistent", result.Summary);
    }

    private static byte[] CreateFrame(
        byte messageType,
        byte correlationId,
        byte[] payload)
    {
        byte[] frame = new byte[8 + payload.Length];
        frame[0] = CompactSerialProtocolV1Inspection.StartMarkerFirstByte;
        frame[1] = CompactSerialProtocolV1Inspection.StartMarkerSecondByte;
        frame[2] = CompactSerialProtocolV1Inspection.ProtocolVersion;
        frame[3] = messageType;
        frame[4] = correlationId;
        frame[5] = checked((byte)payload.Length);
        payload.CopyTo(frame, 6);
        RewriteCrc(frame);
        return frame;
    }

    private static void RewriteCrc(byte[] frame)
    {
        int payloadLength = frame[5];
        ushort crc = CompactSerialProtocolV1Inspection.CalculateCrc(
            frame.AsSpan(2, 4 + payloadLength));
        BinaryPrimitives.WriteUInt16BigEndian(
            frame.AsSpan(6 + payloadLength, 2),
            crc);
    }

    private static RuntimeDiagnosticByteSnapshot Snapshot(byte[] frame)
    {
        return new RuntimeDiagnosticByteSnapshot(frame.Length, frame, isTruncated: false);
    }

    private static void AssertField(
        DesktopRuntimeByteInterpretation result,
        int offset,
        int length,
        string name,
        string value,
        string hex)
    {
        DesktopRuntimeByteField field =
            result.Fields.Single(candidate => candidate.Name == name);
        Assert.Equal(offset, field.Offset);
        Assert.Equal(length, field.Length);
        Assert.Equal(value, field.InterpretedValue);
        Assert.Equal(hex, field.ByteHex);
        Assert.Equal(DesktopRuntimeByteFieldValidation.Valid, field.Validation);
    }
}
