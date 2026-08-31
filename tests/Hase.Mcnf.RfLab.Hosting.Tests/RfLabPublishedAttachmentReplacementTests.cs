using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Mcnf.RfLab.Hosting.Tests;

public sealed class RfLabPublishedAttachmentReplacementTests
{
    private static async Task<(
        RfLabPublishedAttachment Attachment,
        ScriptedSerialByteStream FirstStream,
        RecordingSerialFactory SerialFactory)>
        OpenAsync(params Hase.Transport.Serial.ISerialByteStream[] laterStreams)
    {
        var context = new RuntimeContext();
        var firstStream = RfLabHostingTestSupport.SuccessfulOpenStream(
            [0x20]); // one failing connectivity probe after the open sequence
        var streams = new List<Hase.Transport.Serial.ISerialByteStream> { firstStream };
        streams.AddRange(laterStreams);
        var serialFactory = new RecordingSerialFactory([.. streams]);
        var factory = new RfLabPublishedAttachmentFactory(
            context,
            serialFactory,
            settleDelay: TimeSpan.Zero);

        RfLabPublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());
        return (attachment, firstStream, serialFactory);
    }

    [Fact]
    public async Task ReplaceAsync_RequiresAFaultedEndpoint()
    {
        (RfLabPublishedAttachment attachment, _, _) = await OpenAsync();
        await using RfLabPublishedAttachment owned = attachment;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => attachment.ReplaceAsync(RfLabHostingTestSupport.SupportedOptions()));
    }

    [Fact]
    public async Task ReplaceAsync_RecoversAFaultedEndpointWithAFreshConnection()
    {
        (RfLabPublishedAttachment attachment, ScriptedSerialByteStream firstStream,
            RecordingSerialFactory serialFactory) =
            await OpenAsync(RfLabHostingTestSupport.SuccessfulOpenStream());
        await using RfLabPublishedAttachment owned = attachment;

        // The scripted probe response is not the connectivity byte, so the
        // passive probe faults the endpoint.
        await Assert.ThrowsAsync<InvalidDataException>(
            () => attachment.ProbeHealthAsync());
        Assert.Equal(
            EndpointConnectionState.Faulted,
            attachment.RuntimeEndpoint.ConnectionStatus.State);

        await attachment.ReplaceAsync(RfLabHostingTestSupport.SupportedOptions());

        Assert.Equal(
            EndpointConnectionState.Ready,
            attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(2, serialFactory.OpenCount);
        Assert.Equal(1, firstStream.DisposeCount);
    }

    [Fact]
    public async Task ReplaceAsync_FailedReplacementLeavesTheEndpointFaulted()
    {
        var failingStream = new ScriptedSerialByteStream([0x20]);
        (RfLabPublishedAttachment attachment, _, _) = await OpenAsync(failingStream);
        await using RfLabPublishedAttachment owned = attachment;

        await Assert.ThrowsAsync<InvalidDataException>(
            () => attachment.ProbeHealthAsync());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => attachment.ReplaceAsync(RfLabHostingTestSupport.SupportedOptions()));

        Assert.Equal(
            EndpointConnectionState.Faulted,
            attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(1, failingStream.DisposeCount);
    }

    [Fact]
    public async Task Operations_ReportUnavailableAfterDisposal()
    {
        (RfLabPublishedAttachment attachment, _, _) = await OpenAsync();
        await attachment.DisposeAsync();

        var readResult = await attachment.PropertyOperations.ReadAsync(
            new InstrumentId("rf-minilab-01"),
            new PropertyId("sensor-voltage"));
        var commandResult = await attachment.CommandOperations.ExecuteAsync(
            new InstrumentId("rf-minilab-01"),
            RfLabCommandMapping.ApplyCarrier.CommandPath,
            argument: null);

        Assert.False(readResult.IsSuccess);
        Assert.False(commandResult.IsSuccess);
    }
}
