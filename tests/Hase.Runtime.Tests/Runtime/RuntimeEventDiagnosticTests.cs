using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Runtime;

public sealed class RuntimeEventDiagnosticTests
{
    [Fact]
    public void PublishOccurrence_PublishesPayloadFreeStructuralRecord()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent(
                collector);

        runtimeEvent.PublishOccurrence(
            DateTimeOffset.UtcNow,
            "sensitive event payload");

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot());

        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            record.Level);
        Assert.Equal(
            RuntimeDiagnosticCategory.RuntimeEvent,
            record.Category);
        Assert.Equal(
            "EventOccurred",
            record.EventName);
        Assert.Equal(
            "endpoint-one",
            record.EndpointId);
        Assert.Equal(
            "instrument-one",
            record.Details["instrument"]);
        Assert.Equal(
            "Button.Pressed",
            record.Details["path"]);
        Assert.Null(
            record.AttachmentGeneration);
        Assert.Null(
            record.OperationId);
        Assert.Null(
            record.Duration);
        Assert.Null(
            record.Outcome);
        Assert.DoesNotContain(
            record.Details,
            detail =>
                detail.Value.Contains(
                    "sensitive",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublishOccurrence_MultipleObserversStillPublishOneRecord()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent(
                collector);

        var firstObserver =
            new RecordingObserver();

        var secondObserver =
            new RecordingObserver();

        runtimeEvent.Subscribe(
            firstObserver);

        runtimeEvent.Subscribe(
            secondObserver);

        runtimeEvent.PublishOccurrence(
            DateTimeOffset.UtcNow,
            value: null);

        Assert.Single(
            collector.GetSnapshot());
        Assert.Equal(
            1,
            firstObserver.CallCount);
        Assert.Equal(
            1,
            secondObserver.CallCount);
    }

    [Fact]
    public void PublishOccurrence_ThrowingDiagnosticSinkDoesNotBlockObservers()
    {
        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent(
                new ThrowingSink());

        var observer =
            new RecordingObserver();

        runtimeEvent.Subscribe(
            observer);

        Exception? exception =
            Record.Exception(
                () =>
                    runtimeEvent.PublishOccurrence(
                        DateTimeOffset.UtcNow,
                        true));

        Assert.Null(
            exception);
        Assert.Equal(
            1,
            observer.CallCount);
    }

    private static RuntimeEvent CreateRuntimeEvent(
        IRuntimeDiagnosticSink sink)
    {
        RuntimeContext context =
            new(
                new RuntimeDiagnosticPublisher(
                    sink));

        var eventDescriptor =
            new EventDescriptor(
                new DescriptorPath(
                    "Button",
                    "Pressed"),
                "Button pressed");

        var instrumentDescriptor =
            new InstrumentDescriptor(
                new InstrumentId(
                    "instrument-one"),
                "Instrument",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            eventDescriptor
                        ])
            };

        RuntimeEndpoint endpoint =
            context.CreateEndpoint(
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-one"),
                    [
                        instrumentDescriptor
                    ]));

        return Assert.Single(
            Assert.Single(
                    endpoint.Instruments)
                .Events);
    }

    private sealed class RecordingObserver :
        IRuntimeEventObserver
    {
        public int CallCount
        {
            get;
            private set;
        }

        public void OnRuntimeEventOccurred(
            RuntimeEventOccurrence occurrence)
        {
            CallCount++;
        }
    }

    private sealed class ThrowingSink :
        IRuntimeDiagnosticSink
    {
        public bool IsEnabled(
            RuntimeDiagnosticLevel level)
        {
            return true;
        }

        public void Publish(
            RuntimeDiagnosticRecord record)
        {
            throw new InvalidOperationException(
                "Observer failure.");
        }
    }
}
