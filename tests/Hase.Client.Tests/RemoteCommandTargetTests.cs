using Hase.Client;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests;

public sealed class RemoteCommandTargetTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveGenerationScopedIdentity()
    {
        var attachment =
            CreateAttachment();
        var instrumentId =
            new InstrumentId(
                "instrument-01");
        var commandPath =
            new DescriptorPath(
                "Led",
                "Toggle");

        var target =
            new RemoteCommandTarget(
                attachment,
                instrumentId,
                commandPath);

        Assert.Same(
            attachment,
            target.Attachment);
        Assert.Same(
            attachment.EndpointId,
            target.EndpointId);
        Assert.Same(
            attachment.Generation,
            target.AttachmentGeneration);
        Assert.Same(
            instrumentId,
            target.InstrumentId);
        Assert.Same(
            commandPath,
            target.CommandPath);
    }

    [Fact]
    public void Constructor_NullAttachment_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "attachment",
            () => new RemoteCommandTarget(
                null!,
                new InstrumentId(
                    "instrument-01"),
                new DescriptorPath(
                    "Led",
                    "Toggle")));
    }

    [Fact]
    public void Constructor_NullInstrumentId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "instrumentId",
            () => new RemoteCommandTarget(
                CreateAttachment(),
                null!,
                new DescriptorPath(
                    "Led",
                    "Toggle")));
    }

    [Fact]
    public void Constructor_NullCommandPath_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "commandPath",
            () => new RemoteCommandTarget(
                CreateAttachment(),
                new InstrumentId(
                    "instrument-01"),
                null!));
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        RemoteEndpointAttachmentKey attachment =
            CreateAttachment();

        var first =
            new RemoteCommandTarget(
                attachment,
                new InstrumentId(
                    "instrument-01"),
                new DescriptorPath(
                    "Led",
                    "Toggle"));
        var second =
            new RemoteCommandTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId(
                        attachment.EndpointId.Value),
                    new RemoteEndpointAttachmentGeneration(
                        attachment.Generation.Value)),
                new InstrumentId(
                    "instrument-01"),
                new DescriptorPath(
                    "Led",
                    "Toggle"));

        Assert.Equal(
            first,
            second);
    }

    private static RemoteEndpointAttachmentKey CreateAttachment()
    {
        return new RemoteEndpointAttachmentKey(
            new EndpointId(
                "endpoint-01"),
            new RemoteEndpointAttachmentGeneration(
                Guid.Parse(
                    "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8")));
    }
}
