using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.ProtocolExplorer.Scenarios;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class RuntimeHostObservationFormatterTests
{
    private static readonly EndpointId EndpointId =
        new(
            "endpoint-one");

    private static readonly RuntimeEndpointAttachmentGeneration Generation =
        RuntimeEndpointAttachmentGeneration.CreateNew();

    [Fact]
    public void Format_AttachmentPublished_IncludesCommonAndConnectionFields()
    {
        var snapshot =
            new PublishedRuntimeEndpointSnapshot(
                Generation,
                new EndpointDescriptor(
                    EndpointId),
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready));

        string text =
            RuntimeHostObservationFormatter.Format(
                CreateObservation(
                    new RuntimeHostAttachmentPublishedObservationPayload(
                        snapshot)));

        Assert.Contains(
            "Sequence              : 7",
            text);

        Assert.Contains(
            "Kind                  : AttachmentPublished",
            text);

        Assert.Contains(
            "Endpoint              : endpoint-one",
            text);

        Assert.Contains(
            $"Attachment generation : {Generation}",
            text);

        Assert.Contains(
            "Connection state      : Ready",
            text);
    }

    [Fact]
    public void Format_AttachmentEnded_IncludesUtcEndTime()
    {
        DateTimeOffset endedAtUtc =
            CreateUtcTime(
                1);

        string text =
            RuntimeHostObservationFormatter.Format(
                CreateObservation(
                    new RuntimeHostAttachmentEndedObservationPayload(
                        endedAtUtc)));

        Assert.Contains(
            "Kind                  : AttachmentEnded",
            text);

        Assert.Contains(
            $"Ended at UTC          : {endedAtUtc:O}",
            text);
    }

    [Fact]
    public void Format_ConnectionStatusChanged_IncludesTransitionAndDetail()
    {
        string text =
            RuntimeHostObservationFormatter.Format(
                CreateObservation(
                    new RuntimeHostConnectionStatusChangedObservationPayload(
                        new EndpointConnectionStatus(
                            EndpointConnectionState.Connecting),
                        new EndpointConnectionStatus(
                            EndpointConnectionState.Faulted,
                            detail:
                                "Probe timed out."))));

        Assert.Contains(
            "Connection transition : Connecting -> Faulted",
            text);

        Assert.Contains(
            "Connection detail     : Probe timed out.",
            text);
    }

    [Fact]
    public void Format_PropertyValueChanged_IncludesIdentityAndValues()
    {
        string text =
            RuntimeHostObservationFormatter.Format(
                CreateObservation(
                    new RuntimeHostPropertyValueChangedObservationPayload(
                        new InstrumentId(
                            "controller-one"),
                        new PropertyId(
                            "controller.state"),
                        new PropertyValue(
                            false,
                            CreateUtcTime(
                                2)),
                        new PropertyValue(
                            true,
                            CreateUtcTime(
                                3)))));

        Assert.Contains(
            "Instrument            : controller-one",
            text);

        Assert.Contains(
            "Property              : controller.state",
            text);

        Assert.Contains(
            "Previous value        : False",
            text);

        Assert.Contains(
            "Current value         : True",
            text);
    }

    [Fact]
    public void Format_EventOccurred_IncludesIdentityUtcTimeAndNullValue()
    {
        DateTimeOffset occurredAtUtc =
            CreateUtcTime(
                4);

        string text =
            RuntimeHostObservationFormatter.Format(
                CreateObservation(
                    new RuntimeHostEventOccurredObservationPayload(
                        new InstrumentId(
                            "controller-one"),
                        new DescriptorPath(
                            "Button",
                            "Pressed"),
                        occurredAtUtc,
                        null)));

        Assert.Contains(
            "Instrument            : controller-one",
            text);

        Assert.Contains(
            "Event                 : Button.Pressed",
            text);

        Assert.Contains(
            $"Occurred at UTC       : {occurredAtUtc:O}",
            text);

        Assert.Contains(
            "Value                 : <null>",
            text);
    }

    [Fact]
    public void Format_NullObservation_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostObservationFormatter.Format(
                null!));
    }

    private static RuntimeHostObservation CreateObservation(
        RuntimeHostObservationPayload payload)
    {
        return new RuntimeHostObservation(
            new RuntimeHostObservationSequence(
                7),
            EndpointId,
            Generation,
            payload);
    }

    private static DateTimeOffset CreateUtcTime(
        int second)
    {
        return new DateTimeOffset(
            2026,
            7,
            24,
            19,
            30,
            second,
            TimeSpan.Zero);
    }
}