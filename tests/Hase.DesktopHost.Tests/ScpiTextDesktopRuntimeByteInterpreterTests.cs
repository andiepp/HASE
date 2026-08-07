using System.Text;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class ScpiTextDesktopRuntimeByteInterpreterTests
{
    private readonly ScpiTextDesktopRuntimeByteInterpreter interpreter = new();

    [Fact]
    public void ProtocolFamily_MatchesProductionDiagnosticDiscriminator()
    {
        Assert.Equal("ScpiText", interpreter.ProtocolFamily);
    }

    [Fact]
    public void Interpret_QueryRequest_ProjectsBodyClassificationAndCr()
    {
        DesktopRuntimeByteInterpretation result = Interpret("*IDN?\r");

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
        Assert.Equal("Valid SCPI query request — CR terminator.", result.Summary);
        AssertField(
            result,
            "Message body",
            0,
            5,
            "*IDN? — 5 ASCII character(s)",
            "2A 49 44 4E 3F");
        AssertField(
            result,
            "Message classification",
            0,
            5,
            "Query request",
            "2A 49 44 4E 3F");
        AssertField(
            result,
            "Terminator",
            5,
            1,
            "CR (0D) — SCPI request terminator",
            "0D");
    }

    [Fact]
    public void Interpret_CommandRequest_RecognizesNonQueryBody()
    {
        DesktopRuntimeByteInterpretation result = Interpret(":INPut OFF\r");

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
        Assert.Equal(
            "Command request",
            Field(result, "Message classification").InterpretedValue);
        Assert.Equal("Valid SCPI command request — CR terminator.", result.Summary);
    }

    [Fact]
    public void Interpret_Response_RecognizesLfTerminator()
    {
        DesktopRuntimeByteInterpretation result = Interpret("0.1000A\n");

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
        Assert.Equal("Response", Field(result, "Message classification").InterpretedValue);
        Assert.Equal(
            "LF (0A) — SCPI response terminator",
            Field(result, "Terminator").InterpretedValue);
        Assert.Equal("Valid SCPI response — LF terminator.", result.Summary);
    }

    [Fact]
    public void Interpret_MissingTerminator_IsMalformed()
    {
        DesktopRuntimeByteInterpretation result = Interpret("*IDN?");

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedMalformedOrIncomplete,
            result.Status);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            Field(result, "Terminator").Validation);
        Assert.Contains("Missing", Field(result, "Terminator").InterpretedValue);
    }

    [Fact]
    public void Interpret_TruncatedBeforeTerminator_IsIncomplete()
    {
        byte[] captured = Encoding.ASCII.GetBytes(new string('A', 256));
        var snapshot = new RuntimeDiagnosticByteSnapshot(
            300,
            captured,
            isTruncated: true);

        DesktopRuntimeByteInterpretation result = interpreter.Interpret(snapshot);

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedMalformedOrIncomplete,
            result.Status);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Incomplete,
            Field(result, "Terminator").Validation);
        Assert.Contains("captured 256 of 300", result.Summary);
    }

    [Fact]
    public void Interpret_NonAsciiBody_IsMalformedWithoutDecodedReplacementText()
    {
        byte[] bytes = [0x41, 0xFF, 0x0A];

        DesktopRuntimeByteInterpretation result = interpreter.Interpret(Snapshot(bytes));

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedMalformedOrIncomplete,
            result.Status);
        DesktopRuntimeByteField body = Field(result, "Message body");
        Assert.Equal(DesktopRuntimeByteFieldValidation.Invalid, body.Validation);
        Assert.Equal("41 FF", body.ByteHex);
        Assert.Contains("unsupported", body.InterpretedValue);
        Assert.DoesNotContain("?", body.InterpretedValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpret_EmptyBody_IsMalformed()
    {
        DesktopRuntimeByteInterpretation result = Interpret("\n");

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedMalformedOrIncomplete,
            result.Status);
        Assert.Equal(
            DesktopRuntimeByteFieldValidation.Invalid,
            Field(result, "Message body").Validation);
        Assert.Contains("Empty", Field(result, "Message body").InterpretedValue);
    }

    [Fact]
    public void Interpret_BytesAfterFirstTerminator_AreReportedAsTrailing()
    {
        DesktopRuntimeByteInterpretation result = Interpret("OK\nEXTRA");

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedMalformedOrIncomplete,
            result.Status);
        DesktopRuntimeByteField trailing = Field(result, "Trailing bytes");
        Assert.Equal(3, trailing.Offset);
        Assert.Equal(5, trailing.Length);
        Assert.Equal(DesktopRuntimeByteFieldValidation.Invalid, trailing.Validation);
        Assert.Equal("45 58 54 52 41", trailing.ByteHex);
    }

    [Fact]
    public void DefaultService_RecognizesScpiText()
    {
        DesktopRuntimeByteInterpretation result =
            DesktopRuntimeByteInterpretationService
                .CreateDefault()
                .Interpret("ScpiText", Snapshot("*IDN?\r"u8.ToArray()));

        Assert.True(result.IsRecognized);
        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
        Assert.Equal("ScpiText", result.ProtocolFamily);
    }

    [Fact]
    public void Interpret_FieldsOwnTheirCapturedBytes()
    {
        byte[] source = "OK\n"u8.ToArray();
        RuntimeDiagnosticByteSnapshot snapshot = Snapshot(source);
        DesktopRuntimeByteInterpretation result = interpreter.Interpret(snapshot);

        source[0] = 0x58;
        source[1] = 0x58;

        Assert.Equal("4F 4B", Field(result, "Message body").ByteHex);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<byte>)Field(result, "Message body").Bytes)[0] = 0x58);
    }

    private DesktopRuntimeByteInterpretation Interpret(string value) =>
        interpreter.Interpret(Snapshot(Encoding.ASCII.GetBytes(value)));

    private static RuntimeDiagnosticByteSnapshot Snapshot(byte[] bytes) =>
        new(bytes.Length, bytes, isTruncated: false);

    private static DesktopRuntimeByteField Field(
        DesktopRuntimeByteInterpretation result,
        string name) =>
        result.Fields.Single(field => field.Name == name);

    private static void AssertField(
        DesktopRuntimeByteInterpretation result,
        string name,
        int offset,
        int length,
        string interpretedValue,
        string byteHex)
    {
        DesktopRuntimeByteField field = Field(result, name);
        Assert.Equal(offset, field.Offset);
        Assert.Equal(length, field.Length);
        Assert.Equal(interpretedValue, field.InterpretedValue);
        Assert.Equal(byteHex, field.ByteHex);
        Assert.Equal(DesktopRuntimeByteFieldValidation.Valid, field.Validation);
    }
}
