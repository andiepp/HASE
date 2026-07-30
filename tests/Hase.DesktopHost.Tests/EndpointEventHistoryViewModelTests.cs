using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.ViewModels;
using Hase.Operator.Presentation;
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
                observation,
                CreateEventDescriptor(
                    "Controller.ButtonPressed",
                    "Button pressed",
                    CreateNumericDescriptor()));

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
            "Button pressed",
            occurrence.EventDisplayName);
        Assert.Equal(
            "Reported value",
            occurrence.PayloadDisplayName);
        Assert.Equal(
            "12.5",
            occurrence.PayloadText);
        Assert.Equal(
            EventPayloadFormatStatus.Formatted,
            occurrence.PayloadStatus);
        Assert.Equal(
            string.Empty,
            occurrence.PayloadDiagnostic);
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
                    null),
                CreateEventDescriptor(
                    "Controller.ButtonPressed",
                    "Button pressed"));
        DesktopRuntimeEventOccurrence esp32 =
            DesktopRuntimeEventOccurrenceProjector.Project(
                CreateObservation(
                    "doit-esp32-devkitc-v4-01",
                    "5cc3e26e-a2c8-43c6-925f-3de8293ee99d",
                    "controller-01",
                    "Controller.ButtonPressed",
                    true),
                CreateEventDescriptor(
                    "Controller.ButtonPressed",
                    "Button pressed",
                    new BooleanDataDescriptor()));

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

    [Theory]
    [MemberData(nameof(PayloadPresentationCases))]
    public void Project_Payload_ShouldUseDescriptorDrivenPresentation(
        DataDescriptor? data,
        object? value,
        string expectedText,
        EventPayloadFormatStatus expectedStatus)
    {
        EventDescriptor? descriptor =
            data is null
                ? null
                : CreateEventDescriptor(
                    "Controller.Event",
                    "Event",
                    data);
        DesktopRuntimeEventOccurrence occurrence =
            DesktopRuntimeEventOccurrenceProjector.Project(
                CreateObservation(
                    "endpoint-01",
                    "e475e69a-bf4a-4e16-9aca-9477a9350d97",
                    "controller-01",
                    "Controller.Event",
                    value),
                descriptor);

        Assert.Equal(
            expectedText,
            occurrence.PayloadText);
        Assert.Equal(
            expectedStatus,
            occurrence.PayloadStatus);
        Assert.Equal(
            expectedStatus is EventPayloadFormatStatus.Formatted
                or EventPayloadFormatStatus.NoPayload
                    ? string.Empty
                    : expectedStatus.ToString(),
            occurrence.PayloadDiagnostic);
    }

    public static TheoryData<
        DataDescriptor?,
        object?,
        string,
        EventPayloadFormatStatus> PayloadPresentationCases =>
        new()
        {
            {
                new BooleanDataDescriptor(),
                true,
                "True",
                EventPayloadFormatStatus.Formatted
            },
            {
                new StringDataDescriptor(),
                "ready",
                "ready",
                EventPayloadFormatStatus.Formatted
            },
            {
                new ByteArrayDataDescriptor(),
                new ByteArrayValue(
                    new byte[]
                    {
                        0x00,
                        0x53,
                        0xFF
                    }),
                "0053FF",
                EventPayloadFormatStatus.Formatted
            },
            {
                new BooleanDataDescriptor(),
                null,
                "Missing payload",
                EventPayloadFormatStatus.MissingPayload
            },
            {
                null,
                true,
                "Unexpected payload",
                EventPayloadFormatStatus.UnexpectedPayload
            },
            {
                new BooleanDataDescriptor(),
                "true",
                "Invalid payload",
                EventPayloadFormatStatus.TypeMismatch
            }
        };

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
                    observation,
                    descriptor: null));
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
            "Button pressed",
            string.Empty,
            "Payload",
            string.Empty,
            "No payload",
            EventPayloadFormatStatus.NoPayload);

    private static EventDescriptor CreateEventDescriptor(
        string path,
        string displayName,
        DataDescriptor? data = null)
    {
        var descriptor =
            new EventDescriptor(
                DescriptorPath.Parse(
                    path),
                displayName);

        return data is null
            ? descriptor
            : descriptor with
            {
                Payload =
                    new EventPayloadDescriptor(
                        "Reported value",
                        data)
            };
    }

    private static NumericDataDescriptor CreateNumericDescriptor()
    {
        Quantity quantity =
            new(
                "ratio",
                "Ratio");
        Unit unit =
            new(
                "ratio",
                "Ratio",
                "ratio",
                quantity);

        return new NumericDataDescriptor(
            quantity,
            unit);
    }

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
