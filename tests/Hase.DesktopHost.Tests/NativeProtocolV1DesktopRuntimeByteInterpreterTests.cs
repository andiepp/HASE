using System.Buffers.Binary;
using Hase.Protocol;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class NativeProtocolV1DesktopRuntimeByteInterpreterTests
{
    private readonly NativeProtocolV1DesktopRuntimeByteInterpreter interpreter =
        new();

    [Fact]
    public void ProtocolFamily_ShouldMatchDiagnosticDiscriminator()
    {
        Assert.Equal(
            "NativeProtocolV1",
            interpreter.ProtocolFamily);
    }

    [Fact]
    public void Interpret_ValidFrame_ShouldProjectExactEnvelopeFields()
    {
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadPropertyRequest,
            correlationId: 3,
            payload: [0xAA, 0xBB]);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
        Assert.Equal(7, result.Fields.Count);
        AssertField(result, 0, 1, "Protocol major version", "1 — current", "01");
        AssertField(result, 1, 1, "Protocol minor version", "0 — current", "00");
        AssertField(result, 2, 1, "Message role", "Request (1)", "01");
        AssertField(result, 3, 1, "Message type", "ReadPropertyRequest (10)", "0A");
        AssertField(result, 4, 4, "Correlation ID", "3", "03 00 00 00");
        AssertField(result, 8, 4, "Payload length", "2 bytes", "02 00 00 00");
        AssertField(result, 12, 2, "Payload", "Payload body — 2 bytes", "AA BB");
    }

    [Fact]
    public void Interpret_ZeroCorrelation_ShouldShowNone()
    {
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Notification,
            ProtocolMessageType.EventNotification,
            correlationId: 0,
            payload: []);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            "0 — none",
            result.Fields.Single(field => field.Name == "Correlation ID").InterpretedValue);
    }

    [Fact]
    public void Interpret_IncompleteHeader_ShouldPreservePartialFields()
    {
        byte[] frame = [0x01, 0x00, 0x01, 0x0A, 0x03];

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedMalformedOrIncomplete,
            result.Status);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Incomplete,
            result.Fields.Single(field => field.Name == "Correlation ID").Validation);
        Assert.Equal(
            "03",
            result.Fields.Single(field => field.Name == "Correlation ID").ByteHex);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Incomplete,
            result.Fields.Single(field => field.Name == "Payload length").Validation);
    }

    [Fact]
    public void Interpret_UnsupportedVersion_ShouldMarkVersionInvalid()
    {
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            correlationId: 1,
            payload: []);
        frame[0] = 2;

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields[0].Validation);
        Assert.Contains("expected 1", result.Fields[0].InterpretedValue);
    }

    [Fact]
    public void Interpret_UndefinedRole_ShouldMarkRoleInvalid()
    {
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            correlationId: 1,
            payload: []);
        frame[2] = 0xFF;

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Message role").Validation);
    }

    [Fact]
    public void Interpret_UndefinedMessageType_ShouldMarkTypeInvalid()
    {
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            correlationId: 1,
            payload: []);
        frame[3] = 0xFF;

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Message type").Validation);
    }

    [Fact]
    public void Interpret_DeclaredPayloadExceedsFrame_ShouldMarkBoundaryInvalid()
    {
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            correlationId: 1,
            payload: []);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8, 4), 5);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            result.Fields.Single(field => field.Name == "Payload length").Validation);
        Assert.Contains("exceeds available", result.Fields[5].InterpretedValue);
    }

    [Fact]
    public void Interpret_TrailingBytes_ShouldMarkBoundaryInvalid()
    {
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            correlationId: 1,
            payload: [0xAA]);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8, 4), 0);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        Assert.Contains(
            "trailing frame bytes",
            result.Fields.Single(field => field.Name == "Payload length").InterpretedValue);
    }

    [Fact]
    public void Interpret_TruncatedPayload_ShouldMarkPayloadIncompleteNotInvalid()
    {
        byte[] complete = CreateFrame(
            ProtocolMessageRole.Response,
            ProtocolMessageType.ReadPropertyResponse,
            correlationId: 7,
            payload: Enumerable.Range(0, 300).Select(index => (byte)index).ToArray());
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
        Assert.Contains("structurally consistent", result.Summary);
    }

    [Fact]
    public void Interpret_EmptyPayload_ShouldIncludeZeroLengthPayloadField()
    {
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            correlationId: 1,
            payload: []);

        DesktopRuntimeByteInterpretation result =
            interpreter.Interpret(Snapshot(frame));

        DesktopRuntimeByteField payload =
            result.Fields.Single(field => field.Name == "Payload");
        Assert.Equal(0, payload.Length);
        Assert.Empty(payload.Bytes);
        Assert.Equal(DesktopRuntimeByteFieldValidation.Valid, payload.Validation);
    }

    [Fact]
    public void Service_ShouldDispatchNativeInterpreter()
    {
        var service = new DesktopRuntimeByteInterpretationService([interpreter]);
        byte[] frame = CreateFrame(
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            correlationId: 1,
            payload: []);

        DesktopRuntimeByteInterpretation result =
            service.Interpret("NativeProtocolV1", Snapshot(frame));

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
    }

    private static byte[] CreateFrame(
        ProtocolMessageRole role,
        ProtocolMessageType messageType,
        uint correlationId,
        byte[] payload)
    {
        byte[] frame = new byte[12 + payload.Length];
        frame[0] = ProtocolVersion.Current.Major;
        frame[1] = ProtocolVersion.Current.Minor;
        frame[2] = (byte)role;
        frame[3] = (byte)messageType;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4, 4), correlationId);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8, 4), (uint)payload.Length);
        payload.CopyTo(frame, 12);
        return frame;
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
        string interpretedValue,
        string byteHex)
    {
        DesktopRuntimeByteField field =
            result.Fields.Single(candidate => candidate.Name == name);
        Assert.Equal(offset, field.Offset);
        Assert.Equal(length, field.Length);
        Assert.Equal(interpretedValue, field.InterpretedValue);
        Assert.Equal(byteHex, field.ByteHex);
        Assert.Equal(DesktopRuntimeByteFieldValidation.Valid, field.Validation);
    }
}
