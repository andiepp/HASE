using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class EndpointEventHistoryViewModelTests
{
    [Fact]
    public void Record_ShouldInsertNewestOccurrenceFirst()
    {
        var history =
            new EndpointEventHistoryViewModel();

        history.Record(
            CreateOccurrence(
                "endpoint-1"));
        history.Record(
            CreateOccurrence(
                "endpoint-2"));

        Assert.Equal(
            ["endpoint-2", "endpoint-1"],
            history.Occurrences
                .Select(
                    occurrence =>
                        occurrence.EndpointId)
                .ToArray());
    }

    [Fact]
    public void Record_ShouldRetainLatestOneHundredOccurrences()
    {
        var history =
            new EndpointEventHistoryViewModel();

        for (
            int index = 0;
            index <= EndpointEventHistoryViewModel.Capacity;
            index++)
        {
            history.Record(
                CreateOccurrence(
                    $"endpoint-{index}"));
        }

        Assert.Equal(
            EndpointEventHistoryViewModel.Capacity,
            history.Occurrences.Count);
        Assert.Equal(
            "endpoint-100",
            history.Occurrences[0].EndpointId);
        Assert.Equal(
            "endpoint-1",
            history.Occurrences[^1].EndpointId);
    }

    [Fact]
    public void Record_WithNullOccurrence_ShouldThrow()
    {
        var history =
            new EndpointEventHistoryViewModel();

        Assert.Throws<ArgumentNullException>(
            () =>
                history.Record(
                    null!));
    }

    [Fact]
    public void Project_ShouldCaptureTheObservationSourceAndPayload()
    {
        RuntimeHostObservation observation =
            CreateObservation(
                "esp32-01",
                "bc73526b-f75d-46e2-8392-8e29ad2bb0fe",
                "controller-01",
                "Controller.ButtonPressed",
                12.5m);

        DesktopRuntimeEventOccurrence occurrence =
            DesktopRuntimeEventOccurrenceProjector.Project(
                observation);

        Assert.Equal(
            "esp32-01",
            occurrence.EndpointId);
        Assert.Equal(
            "bc73526b-f75d-46e2-8392-8e29ad2bb0fe",
            occurrence.AttachmentGeneration);
        Assert.Equal(
            "controller-01",
            occurrence.InstrumentId);
        Assert.Equal(
            "Controller.ButtonPressed",
            occurrence.EventPath);
        Assert.Equal(
            "12.5",
            occurrence.Value);
        Assert.Equal(
            "2026-07-28T12:30:00.0000000+00:00",
            occurrence.OccurredAtUtcText);
    }

    [Fact]
    public void Project_ConsecutiveEndpoints_ShouldRetainTheirOwnSources()
    {
        DesktopRuntimeEventOccurrence arduino =
            DesktopRuntimeEventOccurrenceProjector.Project(
                CreateObservation(
                    "arduino-uno-01",
                    "e475e69a-bf4a-4e16-9aca-9477a9350d97",
                    "arduino-uno-controller-01",
                    "Controller.ButtonPressed",
                    null));
        DesktopRuntimeEventOccurrence esp32 =
            DesktopRuntimeEventOccurrenceProjector.Project(
                CreateObservation(
                    "doit-esp32-devkitc-v4-01",
                    "5cc3e26e-a2c8-43c6-925f-3de8293ee99d",
                    "controller-01",
                    "Controller.ButtonPressed",
                    true));

        Assert.Equal(
            "arduino-uno-01",
            arduino.EndpointId);
        Assert.Equal(
            "arduino-uno-controller-01",
            arduino.InstrumentId);
        Assert.Equal(
            "doit-esp32-devkitc-v4-01",
            esp32.EndpointId);
        Assert.Equal(
            "controller-01",
            esp32.InstrumentId);
        Assert.NotEqual(
            arduino.AttachmentGeneration,
            esp32.AttachmentGeneration);
    }

    [Fact]
    public void Project_WithNonEventObservation_ShouldThrow()
    {
        var observation =
            new RuntimeHostObservation(
                new RuntimeHostObservationSequence(1),
                new EndpointId("endpoint-1"),
                new RuntimeEndpointAttachmentGeneration(
                    Guid.Parse(
                        "4245844a-428c-4a47-836b-e54661e85ef1")),
                new TestObservationPayload());

        Assert.Throws<ArgumentException>(
            "observation",
            () =>
                DesktopRuntimeEventOccurrenceProjector.Project(
                    observation));
    }

    private static DesktopRuntimeEventOccurrence CreateOccurrence(
        string endpointId) =>
        new(
            DateTimeOffset.Parse(
                "2026-07-28T12:30:00+00:00"),
            endpointId,
            "generation-1",
            "instrument-1",
            "Controller.ButtonPressed",
            "null");

    private static RuntimeHostObservation CreateObservation(
        string endpointId,
        string generation,
        string instrumentId,
        string eventPath,
        object? value) =>
        new(
            new RuntimeHostObservationSequence(1),
            new EndpointId(
                endpointId),
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse(
                    generation)),
            new RuntimeHostEventOccurredObservationPayload(
                new InstrumentId(
                    instrumentId),
                DescriptorPath.Parse(
                    eventPath),
                DateTimeOffset.Parse(
                    "2026-07-28T12:30:00+00:00"),
                value));

    private sealed record TestObservationPayload
        : RuntimeHostObservationPayload
    {
        public override RuntimeHostObservationKind Kind =>
            RuntimeHostObservationKind.ConnectionStatusChanged;
    }
}
