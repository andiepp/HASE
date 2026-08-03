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

        runtimeInstrument = ValidateEndpoint(runtimeEndpoint);
        properties = runtimeInstrument.Properties.ToDictionary(
            property => property.Descriptor.Id);
    }

    public RuntimeEndpoint RuntimeEndpoint => runtimeEndpoint;

    public bool IsFaulted => sessionAdapter.IsFaulted;

    public async Task<RuntimeEndpoint> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
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
        else
        {
            Kel103MeasurementMapping mapping = Kel103MeasurementMapping.All.Single(
                candidate => candidate.PropertyId == propertyId);
            Kel103MeasurementObservation observation = await sessionAdapter
                .ReadMeasurementAsync(mapping, cancellationToken)
                .ConfigureAwait(false);
            value = CreateValue(observation.Value, observation.TimestampUtc);
        }

        property.UpdateValue(value);
        return property;
    }

    public ValueTask DisposeAsync() => sessionAdapter.DisposeAsync();

    private static RuntimeInstrument ValidateEndpoint(RuntimeEndpoint endpoint)
    {
        RuntimeInstrument instrument = endpoint.Instruments.Count == 1
            ? endpoint.Instruments[0]
            : throw Incompatible();
        var expected = Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Instruments.Single();

        if (instrument.Descriptor.Id != expected.Id
            || instrument.Properties.Count != expected.Interface.Properties.Count
            || instrument.Commands.Count != 0
            || instrument.Events.Count != 0)
        {
            throw Incompatible();
        }

        for (int index = 0; index < instrument.Properties.Count; index++)
        {
            PropertyDescriptor actual = instrument.Properties[index].Descriptor;
            PropertyDescriptor required = expected.Interface.Properties[index];
            if (actual.Id != required.Id
                || actual.Path != required.Path
                || actual.AccessMode != PropertyAccessMode.Read
                || actual.Data.GetType() != required.Data.GetType())
            {
                throw Incompatible();
            }

            if (required.Data is NumericDataDescriptor requiredNumeric)
            {
                var actualNumeric = (NumericDataDescriptor)actual.Data;
                if (actualNumeric.Quantity.Id != requiredNumeric.Quantity.Id
                    || actualNumeric.NativeUnit.Id != requiredNumeric.NativeUnit.Id
                    || actualNumeric.NativeUnit.Symbol != requiredNumeric.NativeUnit.Symbol)
                {
                    throw Incompatible();
                }
            }
        }

        return instrument;
    }

    private static PropertyValue CreateValue(object value, DateTimeOffset timestampUtc) =>
        new(value, timestampUtc, PropertyQuality.Good);

    private static InvalidDataException Incompatible() =>
        new("The staged runtime endpoint is not compatible with the KEL-103 version-2 definition.");
}
