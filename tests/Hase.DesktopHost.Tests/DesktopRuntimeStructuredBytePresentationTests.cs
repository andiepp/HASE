using System.Buffers.Binary;
using System.IO;
using System.Xml.Linq;
using Hase.CompactProtocol;
using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeStructuredBytePresentationTests
{
    [Fact]
    public void DefaultService_ShouldRecognizeNativeProtocolV1()
    {
        DesktopRuntimeByteInterpretation result =
            DesktopRuntimeByteInterpretationService
                .CreateDefault()
                .Interpret(
                    "NativeProtocolV1",
                    Snapshot(
                        CreateNativeFrame()));

        Assert.True(result.IsRecognized);
        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
    }

    [Fact]
    public void DefaultService_ShouldRecognizeCompactSerialProtocolV1()
    {
        DesktopRuntimeByteInterpretation result =
            DesktopRuntimeByteInterpretationService
                .CreateDefault()
                .Interpret(
                    "CompactSerialProtocolV1",
                    Snapshot(
                        CreateCompactFrame()));

        Assert.True(result.IsRecognized);
        Assert.Equal(
            DesktopRuntimeByteInterpretationStatus.RecognizedValid,
            result.Status);
    }

    [Fact]
    public void Project_NativeByteRecord_ShouldAttachStructuredInterpretation()
    {
        RuntimeDiagnosticRecord record =
            PublishByteRecord(
                "NativeProtocolV1",
                CreateNativeFrame());

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(
                record);

        Assert.Equal("NativeProtocolV1", entry.ByteProtocolFamily);
        Assert.Equal("RecognizedValid", entry.ByteInterpretationStatusText);
        Assert.Contains(
            entry.ByteInterpretationFields,
            field => field.Name == "Message type");
        Assert.False(string.IsNullOrEmpty(entry.ByteHex));
    }

    [Fact]
    public void Project_CompactByteRecord_ShouldAttachCrcField()
    {
        RuntimeDiagnosticRecord record =
            PublishByteRecord(
                "CompactSerialProtocolV1",
                CreateCompactFrame());

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(record);

        Assert.Contains(
            entry.ByteInterpretationFields,
            field => field.Name == "CRC-16/CCITT-FALSE");
    }

    [Fact]
    public void Project_UnsupportedFamily_ShouldPreserveRawBytesAndSafeSummary()
    {
        RuntimeDiagnosticRecord record =
            PublishByteRecord(
                "FutureProtocol",
                [0x01, 0x02]);

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(record);

        Assert.Equal("0102", entry.ByteHex);
        Assert.Equal(
            "UnsupportedProtocolFamily",
            entry.ByteInterpretationStatusText);
        Assert.Contains("FutureProtocol", entry.ByteInterpretationSummary);
    }

    [Fact]
    public void Project_NonByteRecord_ShouldExposeNoCapturedBytesResult()
    {
        var session = new DesktopRuntimeDiagnosticSession();
        session.Publisher.Publish(
            new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeConnection,
                "Connected"));

        DesktopRuntimeDiagnosticEntry entry =
            DesktopRuntimeDiagnosticEntryProjector.Project(
                Assert.Single(session.CaptureDiagnostics()));

        Assert.Equal(
            "NoCapturedBytes",
            entry.ByteInterpretationStatusText);
        Assert.Empty(entry.ByteInterpretationFields);
    }

    [Fact]
    public void ViewModel_ShouldUseInjectedSharedInterpretationService()
    {
        var session = new DesktopRuntimeDiagnosticSession(
            RuntimeDiagnosticLevel.Bytes);
        var publisher = new RuntimeTransportByteDiagnosticPublisher(
            session.Publisher,
            "endpoint-one",
            "TestProtocol");
        publisher.Publish(
            RuntimeDiagnosticDirection.Inbound,
            "1",
            () => new byte[] { 0x01 });

        var interpreter = new TestInterpreter();
        var viewModel = new RuntimeDiagnosticsViewModel(
            session,
            new DesktopRuntimeByteInterpretationService([interpreter]));

        viewModel.Refresh();

        Assert.Equal(
            "Injected test interpretation.",
            Assert.Single(viewModel.Entries).ByteInterpretationSummary);
    }

    [Fact]
    public void DiagnosticsWindow_ShouldContainRawAndStructuredPresentation()
    {
        XDocument document = LoadDiagnosticsWindow();
        string content = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("Raw captured bytes", content, StringComparison.Ordinal);
        Assert.Contains("Structured interpretation", content, StringComparison.Ordinal);
        Assert.Contains("ByteInterpretationFields", content, StringComparison.Ordinal);
        Assert.Contains("Interpreted value", content, StringComparison.Ordinal);
        Assert.Contains("Validation", content, StringComparison.Ordinal);
    }

    private static RuntimeDiagnosticRecord PublishByteRecord(
        string protocolFamily,
        byte[] bytes)
    {
        var session = new DesktopRuntimeDiagnosticSession(
            RuntimeDiagnosticLevel.Bytes);
        var publisher = new RuntimeTransportByteDiagnosticPublisher(
            session.Publisher,
            "endpoint-one",
            protocolFamily);
        publisher.Publish(
            RuntimeDiagnosticDirection.Inbound,
            "1",
            () => bytes);
        return Assert.Single(session.CaptureDiagnostics());
    }

    private static byte[] CreateNativeFrame()
    {
        byte[] frame = new byte[12];
        frame[0] = 1;
        frame[1] = 0;
        frame[2] = 1;
        frame[3] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8, 4), 0);
        return frame;
    }

    private static byte[] CreateCompactFrame()
    {
        byte[] frame =
        [
            CompactSerialProtocolV1Inspection.StartMarkerFirstByte,
            CompactSerialProtocolV1Inspection.StartMarkerSecondByte,
            CompactSerialProtocolV1Inspection.ProtocolVersion,
            0x01,
            0x01,
            0x00,
            0x00,
            0x00
        ];
        ushort crc = CompactSerialProtocolV1Inspection.CalculateCrc(
            frame.AsSpan(2, 4));
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(6, 2), crc);
        return frame;
    }

    private static RuntimeDiagnosticByteSnapshot Snapshot(byte[] bytes)
    {
        return new RuntimeDiagnosticByteSnapshot(
            bytes.Length,
            bytes,
            isTruncated: false);
    }

    private static XDocument LoadDiagnosticsWindow()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory,
                "src",
                "Hase.DesktopHost.App",
                "Views",
                "DiagnosticsWindow.xaml");
            if (File.Exists(candidate))
            {
                return XDocument.Load(candidate);
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate DiagnosticsWindow.xaml.");
    }

    private sealed class TestInterpreter : IDesktopRuntimeByteInterpreter
    {
        public string ProtocolFamily => "TestProtocol";

        public DesktopRuntimeByteInterpretation Interpret(
            RuntimeDiagnosticByteSnapshot snapshot)
        {
            return new DesktopRuntimeByteInterpretation(
                DesktopRuntimeByteInterpretationStatus.RecognizedValid,
                ProtocolFamily,
                "Injected test interpretation.");
        }
    }
}
