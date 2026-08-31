using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Mcnf.RfLab.Hosting.Tests;

public sealed class RfLabSupervisedAttachmentFactoryTests
{
    [Fact]
    public async Task OpenAsync_PublishesAReadySupervisedEndpoint()
    {
        var context = new RuntimeContext();
        var factory = new RfLabSupervisedAttachmentFactory(
            context,
            new RecordingSerialFactory(RfLabHostingTestSupport.SuccessfulOpenStream()),
            settleDelay: TimeSpan.Zero);

        await using RfLabSupervisedAttachment attachment = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());

        Assert.Same(attachment.RuntimeEndpoint, context.Endpoints.Single());
        Assert.Equal(
            EndpointConnectionState.Ready,
            attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(0, attachment.GetConnectionStatistics().ReconnectAttemptCount);
    }

    [Fact]
    public async Task RecoverySupervision_ReplacesAFaultedConnectionAutomatically()
    {
        var context = new RuntimeContext();
        var factory = new RfLabSupervisedAttachmentFactory(
            context,
            new RecordingSerialFactory(
                RfLabHostingTestSupport.SuccessfulOpenStream([0x20]),
                RfLabHostingTestSupport.SuccessfulOpenStream()),
            reconnectPolicy: new ImmediateReconnectPolicy(),
            settleDelay: TimeSpan.Zero);

        await using RfLabSupervisedAttachment attachment = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());

        // The scripted probe response is not the connectivity byte; the next
        // manual probe faults the endpoint and supervision recovers it.
        try
        {
            await ProbeAsync(attachment);
        }
        catch (InvalidDataException)
        {
        }

        await WaitForStateAsync(
            attachment.RuntimeEndpoint,
            EndpointConnectionState.Ready,
            TimeSpan.FromSeconds(10));

        Assert.Equal(1, attachment.GetConnectionStatistics().SuccessfulRecoveryCount);
    }

    [Fact]
    public async Task DisposeAsync_RemovesTheEndpointAndStopsSupervision()
    {
        var context = new RuntimeContext();
        var factory = new RfLabSupervisedAttachmentFactory(
            context,
            new RecordingSerialFactory(RfLabHostingTestSupport.SuccessfulOpenStream()),
            settleDelay: TimeSpan.Zero);
        RfLabSupervisedAttachment attachment = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());

        await attachment.DisposeAsync();

        Assert.Empty(context.Endpoints);
        Assert.Equal(
            EndpointConnectionState.Disconnected,
            attachment.RuntimeEndpoint.ConnectionStatus.State);
    }

    private static async Task ProbeAsync(RfLabSupervisedAttachment attachment)
    {
        // The supervised attachment exposes no probe; read a property that
        // requires an exchange so the faulted stream surfaces.
        var result = await attachment.PropertyOperations.ReadAsync(
            new Hase.Core.Domain.Identity.InstrumentId("rf-minilab-01"),
            new Hase.Core.Domain.Identity.PropertyId("sensor-voltage"));
        if (!result.IsSuccess)
        {
            throw new InvalidDataException("The probing read failed as scripted.");
        }
    }

    private static async Task WaitForStateAsync(
        RuntimeEndpoint endpoint,
        EndpointConnectionState state,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (endpoint.ConnectionStatus.State != state)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"The endpoint did not reach {state} within {timeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }
    }

    private sealed class ImmediateReconnectPolicy
        : Hase.Runtime.Transport.IRuntimeEndpointReconnectPolicy
    {
        public TimeSpan GetDelay(int retryAttempt) => TimeSpan.Zero;
    }
}
