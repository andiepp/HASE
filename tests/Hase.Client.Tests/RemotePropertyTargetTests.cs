using Hase.Client;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemotePropertyTargetTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveGenerationScopedIdentity()
    {
        var attachment =
            CreateAttachment();
        var instrumentId =
            new InstrumentId(
                "instrument-01");
        var propertyId =
            new PropertyId(
                "property-01");

        var target =
            new RemotePropertyTarget(
                attachment,
                instrumentId,
                propertyId);

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
            propertyId,
            target.PropertyId);
    }

    [Fact]
    public void Constructor_NullAttachment_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "attachment",
            () => new RemotePropertyTarget(
                null!,
                new InstrumentId(
                    "instrument-01"),
                new PropertyId(
                    "property-01")));
    }

    [Fact]
    public void Constructor_NullInstrumentId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "instrumentId",
            () => new RemotePropertyTarget(
                CreateAttachment(),
                null!,
                new PropertyId(
                    "property-01")));
    }

    [Fact]
    public void Constructor_NullPropertyId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "propertyId",
            () => new RemotePropertyTarget(
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
            new RemotePropertyTarget(
                attachment,
                new InstrumentId(
                    "instrument-01"),
                new PropertyId(
                    "property-01"));
        var second =
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId(
                        attachment.EndpointId.Value),
                    new RemoteEndpointAttachmentGeneration(
                        attachment.Generation.Value)),
                new InstrumentId(
                    "instrument-01"),
                new PropertyId(
                    "property-01"));

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
