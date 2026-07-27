using Hase.Client;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemoteStateObservationPayloadTests
{
    [Fact]
    public void ConnectionStatusChanged_Values_ShouldPreservePayload()
    {
        var previousStatus =
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Faulted);
        var currentStatus =
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Reconnecting);

        var payload =
            new RemoteConnectionStatusChangedObservationPayload(
                previousStatus,
                currentStatus);

        Assert.Equal(
            RemoteObservationKind.ConnectionStatusChanged,
            payload.Kind);
        Assert.Same(
            previousStatus,
            payload.PreviousStatus);
        Assert.Same(
            currentStatus,
            payload.CurrentStatus);
    }

    [Fact]
    public void ConnectionStatusChanged_NullPreviousStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "previousStatus",
            () => new RemoteConnectionStatusChangedObservationPayload(
                null!,
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready)));
    }

    [Fact]
    public void ConnectionStatusChanged_NullCurrentStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "currentStatus",
            () => new RemoteConnectionStatusChangedObservationPayload(
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready),
                null!));
    }

    [Fact]
    public void PropertyValueChanged_Values_ShouldPreservePayload()
    {
        var instrumentId =
            new InstrumentId(
                "instrument-01");
        var propertyId =
            new PropertyId(
                "property-01");
        RemotePropertyValue previousValue =
            CreatePropertyValue(
                false);
        RemotePropertyValue currentValue =
            CreatePropertyValue(
                true);

        var payload =
            new RemotePropertyValueChangedObservationPayload(
                instrumentId,
                propertyId,
                previousValue,
                currentValue);

        Assert.Equal(
            RemoteObservationKind.PropertyValueChanged,
            payload.Kind);
        Assert.Same(
            instrumentId,
            payload.InstrumentId);
        Assert.Same(
            propertyId,
            payload.PropertyId);
        Assert.Same(
            previousValue,
            payload.PreviousValue);
        Assert.Same(
            currentValue,
            payload.CurrentValue);
    }

    [Fact]
    public void PropertyValueChanged_UnknownPreviousValue_ShouldSucceed()
    {
        var payload =
            new RemotePropertyValueChangedObservationPayload(
                new InstrumentId(
                    "instrument-01"),
                new PropertyId(
                    "property-01"),
                previousValue: null,
                CreatePropertyValue(
                    true));

        Assert.Null(
            payload.PreviousValue);
    }

    [Fact]
    public void PropertyValueChanged_NullInstrumentId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "instrumentId",
            () => new RemotePropertyValueChangedObservationPayload(
                null!,
                new PropertyId(
                    "property-01"),
                null,
                CreatePropertyValue(
                    true)));
    }

    [Fact]
    public void PropertyValueChanged_NullPropertyId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "propertyId",
            () => new RemotePropertyValueChangedObservationPayload(
                new InstrumentId(
                    "instrument-01"),
                null!,
                null,
                CreatePropertyValue(
                    true)));
    }

    [Fact]
    public void PropertyValueChanged_NullCurrentValue_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "currentValue",
            () => new RemotePropertyValueChangedObservationPayload(
                new InstrumentId(
                    "instrument-01"),
                new PropertyId(
                    "property-01"),
                null,
                null!));
    }

    private static RemotePropertyValue CreatePropertyValue(
        bool value)
    {
        return new RemotePropertyValue(
            RemoteValue.FromBoolean(
                value),
            DateTimeOffset.UnixEpoch,
            RemotePropertyQuality.Good);
    }
}
