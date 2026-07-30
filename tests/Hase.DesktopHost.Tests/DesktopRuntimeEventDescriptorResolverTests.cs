using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeEventDescriptorResolverTests
{
    private static readonly Guid Generation =
        Guid.Parse(
            "f8f76b04-9c42-45cc-94a3-91fbd4c4c6ec");

    [Fact]
    public void Resolve_ExactAttachmentInstrumentAndPath_ShouldReturnDescriptor()
    {
        EventDescriptor descriptor =
            CreateDescriptor();
        PublishedRuntimeHostSnapshot snapshot =
            CreateSnapshot(
                descriptor,
                Generation);

        EventDescriptor? result =
            DesktopRuntimeEventDescriptorResolver.Resolve(
                snapshot,
                CreateObservation(
                    Generation));

        Assert.Same(
            descriptor,
            result);
    }

    [Fact]
    public void Resolve_DifferentAttachmentGeneration_ShouldReturnNull()
    {
        PublishedRuntimeHostSnapshot snapshot =
            CreateSnapshot(
                CreateDescriptor(),
                Guid.Parse(
                    "fdfb6db5-dd22-4e3b-8c47-cbbd82a5817e"));

        EventDescriptor? result =
            DesktopRuntimeEventDescriptorResolver.Resolve(
                snapshot,
                CreateObservation(
                    Generation));

        Assert.Null(
            result);
    }

    [Fact]
    public void Resolve_UnknownEventPath_ShouldReturnNull()
    {
        PublishedRuntimeHostSnapshot snapshot =
            CreateSnapshot(
                CreateDescriptor(),
                Generation);
        RuntimeHostObservation observation =
            CreateObservation(
                Generation,
                "Controller.Unknown");

        Assert.Null(
            DesktopRuntimeEventDescriptorResolver.Resolve(
                snapshot,
                observation));
    }

    [Fact]
    public void Resolve_NonEventObservation_ShouldThrow()
    {
        PublishedRuntimeHostSnapshot snapshot =
            CreateSnapshot(
                CreateDescriptor(),
                Generation);
        var observation =
            new RuntimeHostObservation(
                new RuntimeHostObservationSequence(
                    1),
                new EndpointId(
                    "endpoint-01"),
                new RuntimeEndpointAttachmentGeneration(
                    Generation),
                new TestObservationPayload());

        Assert.Throws<ArgumentException>(
            "observation",
            () =>
                DesktopRuntimeEventDescriptorResolver.Resolve(
                    snapshot,
                    observation));
    }

    private static PublishedRuntimeHostSnapshot CreateSnapshot(
        EventDescriptor descriptor,
        Guid generation)
    {
        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "controller-01"),
                "Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            descriptor
                        ])
            };
        var endpoint =
            new PublishedRuntimeEndpointSnapshot(
                new RuntimeEndpointAttachmentGeneration(
                    generation),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-01"),
                    [
                        instrument
                    ]),
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready));

        return new PublishedRuntimeHostSnapshot(
            new RuntimeHostId(
                "runtime-host-01"),
            RuntimeHostApiVersion.Current,
            [
                endpoint
            ]);
    }

    private static RuntimeHostObservation CreateObservation(
        Guid generation,
        string eventPath = "Controller.ButtonPressed") =>
        new(
            new RuntimeHostObservationSequence(
                1),
            new EndpointId(
                "endpoint-01"),
            new RuntimeEndpointAttachmentGeneration(
                generation),
            new RuntimeHostEventOccurredObservationPayload(
                new InstrumentId(
                    "controller-01"),
                DescriptorPath.Parse(
                    eventPath),
                DateTimeOffset.UnixEpoch,
                value: null));

    private static EventDescriptor CreateDescriptor() =>
        new(
            DescriptorPath.Parse(
                "Controller.ButtonPressed"),
            "Button pressed");

    private sealed record TestObservationPayload
        : RuntimeHostObservationPayload
    {
        public override RuntimeHostObservationKind Kind =>
            RuntimeHostObservationKind.ConnectionStatusChanged;
    }
}
