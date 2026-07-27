using Hase.Client;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests;

public sealed class RemoteCommandExecutionRequestTests
{
    [Fact]
    public void Constructor_TargetAndArgument_ShouldPreserveRequest()
    {
        RemoteCommandTarget target =
            CreateTarget();
        RemoteValue argument =
            RemoteValue.FromBoolean(
                true);

        var request =
            new RemoteCommandExecutionRequest(
                target,
                argument);

        Assert.Same(
            target,
            request.Target);
        Assert.Same(
            argument,
            request.Argument);
    }

    [Fact]
    public void Constructor_WithoutArgument_ShouldSucceed()
    {
        var request =
            new RemoteCommandExecutionRequest(
                CreateTarget());

        Assert.Null(
            request.Argument);
    }

    [Fact]
    public void Constructor_NullTarget_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "target",
            () => new RemoteCommandExecutionRequest(
                null!));
    }

    private static RemoteCommandTarget CreateTarget()
    {
        return new RemoteCommandTarget(
            new RemoteEndpointAttachmentKey(
                new EndpointId(
                    "endpoint-01"),
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8"))),
            new InstrumentId(
                "instrument-01"),
            new DescriptorPath(
                "Led",
                "Toggle"));
    }
}
