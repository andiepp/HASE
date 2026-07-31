using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeByteInterpretationFoundationTests
{
    [Fact]
    public void Field_ShouldOwnBytesAndFormatSpacedUppercaseHex()
    {
        byte[] source = [0x0A, 0xB5, 0x00];
        var field = new DesktopRuntimeByteField(
            4,
            3,
            " Body ",
            "Payload",
            source,
            DesktopRuntimeByteFieldValidation.Valid);

        source[0] = 0xFF;

        Assert.Equal(4, field.Offset);
        Assert.Equal(3, field.Length);
        Assert.Equal("Body", field.Name);
        Assert.Equal("Payload", field.InterpretedValue);
        Assert.Equal("0A B5 00", field.ByteHex);
        Assert.Equal(DesktopRuntimeByteFieldValidation.Valid, field.Validation);
    }

    [Fact]
    public void Field_ShouldAllowIncompleteCapturedRange()
    {
        var field = new DesktopRuntimeByteField(
            8,
            4,
            "CRC",
            "Only one of four bytes captured",
            [0x12],
            DesktopRuntimeByteFieldValidation.Incomplete);

        Assert.Equal(4, field.Length);
        Assert.Single(field.Bytes);
    }

    [Fact]
    public void Field_BytesLongerThanDeclaredLength_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "capturedBytes",
            () => new DesktopRuntimeByteField(
                0,
                1,
                "Field",
                "Value",
                [0x01, 0x02]));
    }

    [Fact]
    public void Interpretation_ShouldOwnFieldsAndExposeRecognition()
    {
        var source = new List<DesktopRuntimeByteField>
        {
            new(0, 1, "Version", "1", [0x01])
        };
        var result = new DesktopRuntimeByteInterpretation(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            "NativeProtocolV1",
            " Valid frame ",
            source);

        source.Clear();

        Assert.True(result.IsRecognized);
        Assert.Equal("Valid frame", result.Summary);
        Assert.Single(result.Fields);
    }

    [Fact]
    public void Service_NoSnapshot_ShouldReturnNoCapturedBytes()
    {
        var service = new DesktopRuntimeByteInterpretationService();

        DesktopRuntimeByteInterpretation result =
            service.Interpret("NativeProtocolV1", null);

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.NoCapturedBytes,
            result.Status);
        Assert.False(result.IsRecognized);
    }

    [Fact]
    public void Service_UnsupportedFamily_ShouldReturnUnsupportedResult()
    {
        var service = new DesktopRuntimeByteInterpretationService();

        DesktopRuntimeByteInterpretation result =
            service.Interpret(
                "UnknownProtocol",
                Snapshot([0x01]));

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.UnsupportedProtocolFamily,
            result.Status);
        Assert.Contains("UnknownProtocol", result.Summary);
    }

    [Fact]
    public void Service_RegisteredFamily_ShouldDispatchExactSnapshot()
    {
        var interpreter = new RecordingInterpreter("NativeProtocolV1");
        var service = new DesktopRuntimeByteInterpretationService([interpreter]);
        RuntimeDiagnosticByteSnapshot snapshot = Snapshot([0x01, 0x02]);

        DesktopRuntimeByteInterpretation result =
            service.Interpret(" NativeProtocolV1 ", snapshot);

        Assert.Same(snapshot, interpreter.Snapshot);
        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
    }

    [Fact]
    public void Service_InterpreterFailure_ShouldReturnSafeMalformedResult()
    {
        var service = new DesktopRuntimeByteInterpretationService(
            [new ThrowingInterpreter()]);

        DesktopRuntimeByteInterpretation result =
            service.Interpret(
                "ThrowingProtocol",
                Snapshot([0x01]));

        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus
                .RecognizedMalformedOrIncomplete,
            result.Status);
        Assert.True(result.IsRecognized);
        Assert.Empty(result.Fields);
    }

    [Fact]
    public void Constructor_DuplicateProtocolFamily_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "interpreters",
            () => new DesktopRuntimeByteInterpretationService(
                [
                    new RecordingInterpreter("NativeProtocolV1"),
                    new RecordingInterpreter("NativeProtocolV1")
                ]));
    }

    [Fact]
    public void FormatHex_EmptyBytes_ShouldReturnEmptyText()
    {
        Assert.Empty(
            DesktopRuntimeByteFormatting.FormatHex([]));
    }

    private static RuntimeDiagnosticByteSnapshot Snapshot(
        byte[] bytes)
    {
        return new RuntimeDiagnosticByteSnapshot(
            bytes.Length,
            bytes,
            isTruncated: false);
    }

    private sealed class RecordingInterpreter(string protocolFamily)
        : IDesktopRuntimeByteInterpreter
    {
        public string ProtocolFamily { get; } = protocolFamily;

        public RuntimeDiagnosticByteSnapshot? Snapshot { get; private set; }

        public DesktopRuntimeByteInterpretation Interpret(
            RuntimeDiagnosticByteSnapshot snapshot)
        {
            Snapshot = snapshot;
            return new DesktopRuntimeByteInterpretation(
                DesktopRuntimeByteInterpretationStatus.RecognizedValid,
                ProtocolFamily,
                "Valid test frame.");
        }
    }

    private sealed class ThrowingInterpreter : IDesktopRuntimeByteInterpreter
    {
        public string ProtocolFamily => "ThrowingProtocol";

        public DesktopRuntimeByteInterpretation Interpret(
            RuntimeDiagnosticByteSnapshot snapshot)
        {
            throw new InvalidOperationException("test failure");
        }
    }
}
