using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEventNotificationResolverTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly DescriptorPath EventPath =
        DescriptorPath.Parse(
            "Controller.ButtonPressed");

    [Fact]
    public void Resolve_KnownNoValueEvent_ShouldReturnNotificationAndMapping()
    {
        CompactEventMapping mapping =
            CreateMapping();

        var resolver =
            new CompactEventNotificationResolver(
                CreateMap(
                    mapping));

        var notification =
            new CompactEventNotification(
                eventId: 0x01,
                ReadOnlyMemory<byte>.Empty);

        CompactMappedEventNotification result =
            resolver.Resolve(
                notification);

        Assert.Same(
            notification,
            result.Notification);

        Assert.Same(
            mapping,
            result.Mapping);

        Assert.Equal(
            InstrumentId,
            result.Mapping.InstrumentId);

        Assert.Equal(
            EventPath,
            result.Mapping.EventPath);

        Assert.Equal(
            CompactEventValueEncoding.None,
            result.Mapping.Encoding);
    }

    [Fact]
    public void Resolve_UnknownEventId_ShouldThrow()
    {
        var resolver =
            new CompactEventNotificationResolver(
                CreateMap(
                    CreateMapping()));

        var notification =
            new CompactEventNotification(
                eventId: 0x02,
                ReadOnlyMemory<byte>.Empty);

        void Act()
        {
            _ =
                resolver.Resolve(
                    notification);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Resolve_NoneEncodingWithValue_ShouldThrow()
    {
        var resolver =
            new CompactEventNotificationResolver(
                CreateMap(
                    CreateMapping()));

        var notification =
            new CompactEventNotification(
                eventId: 0x01,
                value:
                new byte[]
                {
                    0x01
                });

        void Act()
        {
            _ =
                resolver.Resolve(
                    notification);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Resolve_NullNotification_ShouldThrow()
    {
        var resolver =
            new CompactEventNotificationResolver(
                CreateMap(
                    CreateMapping()));

        void Act()
        {
            _ =
                resolver.Resolve(
                    null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullEventMap_ShouldThrow()
    {
        void Act()
        {
            _ =
                new CompactEventNotificationResolver(
                    null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void MappedNotification_NullNotification_ShouldThrow()
    {
        void Act()
        {
            _ =
                new CompactMappedEventNotification(
                    null!,
                    CreateMapping());
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void MappedNotification_NullMapping_ShouldThrow()
    {
        void Act()
        {
            _ =
                new CompactMappedEventNotification(
                    new CompactEventNotification(
                        eventId: 0x01,
                        ReadOnlyMemory<byte>.Empty),
                    null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactEventMapping CreateMapping()
    {
        return new CompactEventMapping(
            compactEventId: 0x01,
            InstrumentId,
            EventPath,
            CompactEventValueEncoding.None);
    }

    private static CompactEventMap CreateMap(
        CompactEventMapping mapping)
    {
        var eventDescriptor =
            new EventDescriptor(
                EventPath,
                "Button Pressed");

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            eventDescriptor
                        ])
            };

        var definition =
            new EndpointDescriptorDefinition(
                instruments:
                [
                    instrument
                ],
                metadata:
                    new());

        return new CompactEventMap(
            definition,
            [
                mapping
            ]);
    }
}