using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;

namespace Hase.Scpi.Kel103.Runtime;

public sealed class Kel103RuntimeEndpointAdapter : IAsyncDisposable
{
    private readonly Kel103ReadOnlySessionAdapter sessionAdapter;
    private readonly RuntimeEndpoint runtimeEndpoint;
    private readonly RuntimeInstrument runtimeInstrument;
    private readonly IReadOnlyDictionary<PropertyId, RuntimeProperty> properties;
    private readonly bool supportsOperatingState;
    private readonly TimeProvider timeProvider;

    public Kel103RuntimeEndpointAdapter(
        Kel103ReadOnlySessionAdapter sessionAdapter,
        RuntimeEndpoint runtimeEndpoint,
        TimeProvider? timeProvider = null)
    {
        this.sessionAdapter = sessionAdapter
            ?? throw new ArgumentNullException(nameof(sessionAdapter));
        this.runtimeEndpoint = runtimeEndpoint
            ?? throw new ArgumentNullException(nameof(runtimeEndpoint));
        this.timeProvider = timeProvider ?? TimeProvider.System;

        (runtimeInstrument, supportsOperatingState) = ValidateEndpoint(runtimeEndpoint);
        properties = runtimeInstrument.Properties.ToDictionary(
            property => property.Descriptor.Id);
    }

    public RuntimeEndpoint RuntimeEndpoint => runtimeEndpoint;

    public bool IsFaulted => sessionAdapter.IsFaulted;

    public async Task<RuntimeEndpoint> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
        if (supportsOperatingState)
        {
            return await SynchronizeOperatingStateAsync(cancellationToken).ConfigureAwait(false);
        }

        Kel103SynchronizationSnapshot snapshot = await sessionAdapter
            .VerifyAndSynchronizeAsync(cancellationToken)
            .ConfigureAwait(false);

        var values = new Dictionary<PropertyId, PropertyValue>
        {
            [new PropertyId("product-identity")] = CreateValue(snapshot.Identity.ProductIdentity, snapshot.TimestampUtc),
            [new PropertyId("firmware-version")] = CreateValue(snapshot.Identity.FirmwareVersion, snapshot.TimestampUtc),
            [Kel103MeasurementMapping.Voltage.PropertyId] = CreateValue(snapshot.Voltage, snapshot.TimestampUtc),
            [Kel103MeasurementMapping.Current.PropertyId] = CreateValue(snapshot.Current, snapshot.TimestampUtc),
            [Kel103MeasurementMapping.Power.PropertyId] = CreateValue(snapshot.Power, snapshot.TimestampUtc)
        };

        foreach (RuntimeProperty property in runtimeInstrument.Properties)
        {
            property.UpdateValue(values[property.Descriptor.Id]);
        }

        return runtimeEndpoint;
    }

    public async Task<RuntimeProperty> ReadAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(propertyId);

        if (instrumentId != runtimeInstrument.Descriptor.Id
            || !properties.TryGetValue(propertyId, out RuntimeProperty? property))
        {
            throw new KeyNotFoundException(
                "The requested KEL-103 runtime Property is not supported.");
        }

        PropertyValue value;
        if (propertyId == new PropertyId("product-identity")
            || propertyId == new PropertyId("firmware-version"))
        {
            Kel103Identity identity = await sessionAdapter
                .ReadIdentityAsync(cancellationToken)
                .ConfigureAwait(false);
            object identityValue = propertyId == new PropertyId("product-identity")
                ? identity.ProductIdentity
                : identity.FirmwareVersion;
            value = CreateValue(identityValue, timeProvider.GetUtcNow());
        }
        else if (Kel103MeasurementMapping.All.Any(mapping => mapping.PropertyId == propertyId))
        {
            Kel103MeasurementMapping mapping = Kel103MeasurementMapping.All.Single(
                candidate => candidate.PropertyId == propertyId);
            Kel103MeasurementObservation observation = await sessionAdapter
                .ReadMeasurementAsync(mapping, cancellationToken)
                .ConfigureAwait(false);
            value = CreateValue(observation.Value, observation.TimestampUtc);
        }
        else if (supportsOperatingState && propertyId == Kel103OperatingModeMapping.PropertyId)
        {
            Kel103OperatingModeObservation observation = await sessionAdapter
                .ReadOperatingModeAsync(cancellationToken)
                .ConfigureAwait(false);
            value = CreateValue(
                Kel103OperatingModeMapping.ToNormalizedValue(observation.Mode),
                observation.TimestampUtc);
        }
        else if (supportsOperatingState && propertyId == Kel103InputStateMapping.PropertyId)
        {
            Kel103InputStateObservation observation = await sessionAdapter
                .ReadInputStateAsync(cancellationToken)
                .ConfigureAwait(false);
            value = CreateValue(observation.InputEnabled, observation.TimestampUtc);
        }
        else if (supportsOperatingState)
        {
            Kel103SetpointMapping mapping = Kel103SetpointMapping.All.Single(
                candidate => candidate.PropertyId == propertyId);
            Kel103SetpointObservation observation = await sessionAdapter
                .ReadSetpointAsync(mapping, cancellationToken)
                .ConfigureAwait(false);
            value = CreateValue(observation.Value, observation.TimestampUtc);
        }
        else
        {
            throw new KeyNotFoundException(
                "The requested KEL-103 runtime Property is not supported.");
        }

        property.UpdateValue(value);
        return property;
    }

    public ValueTask DisposeAsync() => sessionAdapter.DisposeAsync();

    private async Task<RuntimeEndpoint> SynchronizeOperatingStateAsync(
        CancellationToken cancellationToken)
    {
        Kel103OperatingStateSynchronizationSnapshot snapshot = await sessionAdapter
            .VerifyAndSynchronizeOperatingStateAsync(cancellationToken)
            .ConfigureAwait(false);

        var values = new Dictionary<PropertyId, PropertyValue>
        {
            [new PropertyId("product-identity")] = CreateValue(snapshot.Identity.ProductIdentity, snapshot.TimestampUtc),
            [new PropertyId("firmware-version")] = CreateValue(snapshot.Identity.FirmwareVersion, snapshot.TimestampUtc),
            [Kel103MeasurementMapping.Voltage.PropertyId] = CreateValue(snapshot.Voltage, snapshot.TimestampUtc),
            [Kel103MeasurementMapping.Current.PropertyId] = CreateValue(snapshot.Current, snapshot.TimestampUtc),
            [Kel103MeasurementMapping.Power.PropertyId] = CreateValue(snapshot.Power, snapshot.TimestampUtc),
            [Kel103OperatingModeMapping.PropertyId] = CreateValue(
                Kel103OperatingModeMapping.ToNormalizedValue(snapshot.OperatingMode),
                snapshot.TimestampUtc),
            [Kel103InputStateMapping.PropertyId] = CreateValue(snapshot.InputEnabled, snapshot.TimestampUtc),
            [Kel103SetpointMapping.Voltage.PropertyId] = CreateValue(snapshot.TargetVoltage, snapshot.TimestampUtc),
            [Kel103SetpointMapping.Current.PropertyId] = CreateValue(snapshot.TargetCurrent, snapshot.TimestampUtc),
            [Kel103SetpointMapping.Resistance.PropertyId] = CreateValue(snapshot.TargetResistance, snapshot.TimestampUtc),
            [Kel103SetpointMapping.Power.PropertyId] = CreateValue(snapshot.TargetPower, snapshot.TimestampUtc)
        };

        foreach (RuntimeProperty property in runtimeInstrument.Properties)
        {
            property.UpdateValue(values[property.Descriptor.Id]);
        }

        return runtimeEndpoint;
    }

    private static (RuntimeInstrument Instrument, bool SupportsOperatingState)
        ValidateEndpoint(RuntimeEndpoint endpoint)
    {
        RuntimeInstrument instrument = endpoint.Instruments.Count == 1
            ? endpoint.Instruments[0]
            : throw Incompatible();

        if (IsCompatible(
                instrument,
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Instruments.Single()))
        {
            return (instrument, false);
        }

        if (IsCompatible(
                instrument,
                Kel103OperatingStateDefinition.EndpointDefinition.Instruments.Single()))
        {
            return (instrument, true);
        }

        throw Incompatible();
    }

    private static bool IsCompatible(
        RuntimeInstrument instrument,
        Hase.Core.Domain.Instruments.InstrumentDescriptor expected)
    {
        if (instrument.Descriptor.Id != expected.Id
            || instrument.Properties.Count != expected.Interface.Properties.Count
            || instrument.Commands.Count != expected.Interface.Commands.Count
            || instrument.Events.Count != expected.Interface.Events.Count)
        {
            return false;
        }

        for (int index = 0; index < instrument.Properties.Count; index++)
        {
            PropertyDescriptor actual = instrument.Properties[index].Descriptor;
            PropertyDescriptor required = expected.Interface.Properties[index];
            if (actual.Id != required.Id
                || actual.Path != required.Path
                || actual.AccessMode != required.AccessMode
                || actual.Data.GetType() != required.Data.GetType())
            {
                return false;
            }

            if (required.Data is NumericDataDescriptor requiredNumeric)
            {
                var actualNumeric = (NumericDataDescriptor)actual.Data;
                if (actualNumeric.Quantity.Id != requiredNumeric.Quantity.Id
                    || actualNumeric.NativeUnit.Id != requiredNumeric.NativeUnit.Id
                    || actualNumeric.NativeUnit.Symbol != requiredNumeric.NativeUnit.Symbol
                    || actualNumeric.Range != requiredNumeric.Range
                    || actualNumeric.Resolution != requiredNumeric.Resolution)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static PropertyValue CreateValue(object value, DateTimeOffset timestampUtc) =>
        new(value, timestampUtc, PropertyQuality.Good);

    private static InvalidDataException Incompatible() =>
        new("The staged runtime endpoint is not compatible with a supported KEL-103 definition.");
}
