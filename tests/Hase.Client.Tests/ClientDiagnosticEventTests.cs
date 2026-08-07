using Hase.Client.Diagnostics;

namespace Hase.Client.Tests;

public sealed class ClientDiagnosticEventTests
{
    [Fact]
    public void Constructor_ByteSnapshotWithoutBytesLevel_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "byteSnapshot",
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Protocol,
                ClientDiagnosticCategory.NorthboundBytes,
                "Bytes",
                byteSnapshot: new RemoteRuntimeDiagnosticByteSnapshot(
                    1,
                    [0x01],
                    false)));
    }

    [Fact]
    public void Constructor_CopiesAndExposesStructuredMetadata()
    {
        Dictionary<string, string> metadata = new()
        {
            ["ApiVersion"] = "1"
        };

        ClientDiagnosticEvent diagnosticEvent =
            new(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientConnection,
                " Connected ",
                metadata: metadata);

        metadata["ApiVersion"] = "changed";

        Assert.Equal("Connected", diagnosticEvent.EventName);
        Assert.Equal("1", diagnosticEvent.Metadata["ApiVersion"]);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, string>)diagnosticEvent.Metadata)
                .Add("Other", "value"));
    }

    [Theory]
    [InlineData("CertificatePassword")]
    [InlineData("Private-Key")]
    [InlineData("AccessToken")]
    [InlineData("RuntimeHostAddress")]
    public void Constructor_SensitiveOrNetworkLocationMetadataKey_Throws(
        string key)
    {
        Dictionary<string, string> metadata = new()
        {
            [key] = "must-not-be-retained"
        };

        Assert.Throws<ArgumentException>(
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientConfiguration,
                "ConfigurationLoaded",
                metadata: metadata));
    }

    [Fact]
    public void Constructor_InvalidInputs_Throw()
    {
        Assert.Throws<ArgumentException>(
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientLifecycle,
                " "));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientLifecycle,
                "Started",
                duration: TimeSpan.FromTicks(-1)));
    }
}
