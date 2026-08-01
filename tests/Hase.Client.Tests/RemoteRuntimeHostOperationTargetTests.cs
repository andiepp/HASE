using Hase.Client;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests;

public sealed class RemoteRuntimeHostOperationTargetTests
{
    [Fact]
    public void PropertyTarget_ShouldPreserveHostAndExactLocalTarget()
    {
        var host = new RemoteRuntimeHostId("host-01");
        var local = new RemotePropertyTarget(CreateAttachment(), new InstrumentId("instrument-01"), new PropertyId("property-01"));
        var target = new RemoteRuntimeHostPropertyTarget(host, local);
        Assert.Same(host, target.RuntimeHostId);
        Assert.Same(local, target.Target);
    }

    [Fact]
    public void PropertyTarget_NullValues_ShouldThrow()
    {
        var local = new RemotePropertyTarget(CreateAttachment(), new InstrumentId("instrument-01"), new PropertyId("property-01"));
        Assert.Throws<ArgumentNullException>("runtimeHostId", () => new RemoteRuntimeHostPropertyTarget(null!, local));
        Assert.Throws<ArgumentNullException>("target", () => new RemoteRuntimeHostPropertyTarget(new RemoteRuntimeHostId("host-01"), null!));
    }

    [Fact]
    public void CommandRequest_ShouldPreserveHostAndExactLocalRequest()
    {
        var host = new RemoteRuntimeHostId("host-01");
        var local = new RemoteCommandExecutionRequest(
            new RemoteCommandTarget(CreateAttachment(), new InstrumentId("instrument-01"), new DescriptorPath("Led", "Toggle")),
            RemoteValue.FromBoolean(true));
        var request = new RemoteRuntimeHostCommandExecutionRequest(host, local);
        Assert.Same(host, request.RuntimeHostId);
        Assert.Same(local, request.Request);
    }

    [Fact]
    public void CommandRequest_NullValues_ShouldThrow()
    {
        var local = new RemoteCommandExecutionRequest(
            new RemoteCommandTarget(CreateAttachment(), new InstrumentId("instrument-01"), new DescriptorPath("Led", "Toggle")));
        Assert.Throws<ArgumentNullException>("runtimeHostId", () => new RemoteRuntimeHostCommandExecutionRequest(null!, local));
        Assert.Throws<ArgumentNullException>("request", () => new RemoteRuntimeHostCommandExecutionRequest(new RemoteRuntimeHostId("host-01"), null!));
    }

    private static RemoteEndpointAttachmentKey CreateAttachment() =>
        new(new EndpointId("endpoint-01"), new RemoteEndpointAttachmentGeneration(Guid.Parse("0a11d9d4-7a02-43be-ae3f-eef9d11e0de8")));
}
