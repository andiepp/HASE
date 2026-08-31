using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;

namespace Hase.Mcnf.RfLab.Runtime;

/// <summary>
/// Adapts one staged RF-Lab runtime endpoint to the session adapter. The
/// writable target properties are staged on the host and pushed to the node
/// by the parameterless apply commands; every other property reads through
/// the serialized session.
/// </summary>
public sealed class RfLabRuntimeEndpointAdapter : IAsyncDisposable
{
    private readonly RfLabSessionAdapter sessionAdapter;
    private readonly RuntimeEndpoint runtimeEndpoint;
    private readonly RuntimeInstrument runtimeInstrument;
    private readonly IReadOnlyDictionary<PropertyId, RuntimeProperty> properties;
    private readonly bool supportsControl;
    private readonly TimeProvider timeProvider;
    private readonly object targetLock = new();
    private readonly Dictionary<PropertyId, double> stagedTargets;

    public RfLabRuntimeEndpointAdapter(
        RfLabSessionAdapter sessionAdapter,
        RuntimeEndpoint runtimeEndpoint,
        TimeProvider? timeProvider = null)
    {
        this.sessionAdapter = sessionAdapter
            ?? throw new ArgumentNullException(nameof(sessionAdapter));
        this.runtimeEndpoint = runtimeEndpoint
            ?? throw new ArgumentNullException(nameof(runtimeEndpoint));
        this.timeProvider = timeProvider ?? TimeProvider.System;

        (runtimeInstrument, supportsControl) = ValidateEndpoint(runtimeEndpoint);
        properties = runtimeInstrument.Properties.ToDictionary(
            property => property.Descriptor.Id);
        stagedTargets = RfLabTargetMapping.All.ToDictionary(
            mapping => mapping.PropertyId,
            mapping => mapping.DefaultValue);
    }

    public RuntimeEndpoint RuntimeEndpoint => runtimeEndpoint;

    public bool IsFaulted => sessionAdapter.IsFaulted;

    public Task ProbeHealthAsync(CancellationToken cancellationToken = default) =>
        sessionAdapter.ProbeHealthAsync(cancellationToken);

    public async Task<RuntimeEndpoint> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
        RfLabSynchronizationSnapshot snapshot = await sessionAdapter
            .VerifyAndSynchronizeAsync(cancellationToken)
            .ConfigureAwait(false);

        var values = new Dictionary<PropertyId, PropertyValue>
        {
            [RfLabProperties.ProductIdentity] =
                CreateValue(RfLabIdentity.ProductIdentity, snapshot.TimestampUtc),
            [RfLabProperties.NodeType] =
                CreateValue(snapshot.Identity.NodeType, snapshot.TimestampUtc),
            [RfLabProperties.SensorLevel] =
                CreateValue(snapshot.Sensor.Level, snapshot.TimestampUtc),
            [RfLabProperties.SensorVoltage] =
                CreateValue(snapshot.Sensor.Millivolts, snapshot.TimestampUtc),
            [RfLabProperties.IndicatorEnabled] =
                CreateValue(snapshot.Configuration.LedOn, snapshot.TimestampUtc),
            [RfLabProperties.ClockGeneratorPresent] =
                CreateValue(snapshot.Configuration.Si5351Present, snapshot.TimestampUtc)
        };

        if (supportsControl)
        {
            lock (targetLock)
            {
                foreach (RfLabTargetMapping mapping in RfLabTargetMapping.All)
                {
                    values[mapping.PropertyId] = CreateValue(
                        stagedTargets[mapping.PropertyId],
                        snapshot.TimestampUtc);
                }
            }
        }

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
                "The requested RF-Lab runtime Property is not supported.");
        }

        PropertyValue value;
        if (propertyId == RfLabProperties.ProductIdentity
            || propertyId == RfLabProperties.NodeType)
        {
            RfLabIdentity identity = await sessionAdapter
                .VerifyIdentityAsync(cancellationToken)
                .ConfigureAwait(false);
            object identityValue = propertyId == RfLabProperties.ProductIdentity
                ? RfLabIdentity.ProductIdentity
                : identity.NodeType;
            value = CreateValue(identityValue, timeProvider.GetUtcNow());
        }
        else if (propertyId == RfLabProperties.SensorLevel
            || propertyId == RfLabProperties.SensorVoltage)
        {
            RfLabSensorObservation observation = await sessionAdapter
                .ReadSensorAsync(cancellationToken)
                .ConfigureAwait(false);
            value = CreateValue(
                propertyId == RfLabProperties.SensorLevel
                    ? observation.Level
                    : observation.Millivolts,
                observation.TimestampUtc);
        }
        else if (propertyId == RfLabProperties.IndicatorEnabled)
        {
            RfLabIndicatorObservation observation = await sessionAdapter
                .ReadIndicatorAsync(cancellationToken)
                .ConfigureAwait(false);
            value = CreateValue(observation.Enabled, observation.TimestampUtc);
        }
        else if (propertyId == RfLabProperties.ClockGeneratorPresent)
        {
            RfLabConfigurationObservation observation = await sessionAdapter
                .ReadConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);
            value = CreateValue(
                observation.Configuration.Si5351Present,
                observation.TimestampUtc);
        }
        else if (supportsControl && TryFindTarget(propertyId, out RfLabTargetMapping? target))
        {
            lock (targetLock)
            {
                value = CreateValue(stagedTargets[target.PropertyId], timeProvider.GetUtcNow());
            }
        }
        else
        {
            throw new KeyNotFoundException(
                "The requested RF-Lab runtime Property is not supported.");
        }

        property.UpdateValue(value);
        return property;
    }

    public Task<RuntimeProperty> WriteAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!supportsControl
            || instrumentId != runtimeInstrument.Descriptor.Id
            || !properties.TryGetValue(propertyId, out RuntimeProperty? property)
            || property.Descriptor.AccessMode != PropertyAccessMode.ReadWrite
            || !TryFindTarget(propertyId, out RfLabTargetMapping? mapping))
        {
            throw new KeyNotFoundException(
                "The requested RF-Lab runtime Property is not writable.");
        }

        double normalizedValue = NormalizeRequestedValue(requestedValue);
        if (normalizedValue < mapping.Minimum || normalizedValue > mapping.Maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedValue),
                "The requested RF-Lab target is outside the characterized range.");
        }

        PropertyValue value;
        lock (targetLock)
        {
            stagedTargets[mapping.PropertyId] = normalizedValue;
            value = CreateValue(normalizedValue, timeProvider.GetUtcNow());
        }

        property.UpdateValue(value);
        return Task.FromResult(property);
    }

    public async Task<RuntimeCommand> ExecuteAsync(
        InstrumentId instrumentId,
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(commandPath);

        RuntimeCommand? command = supportsControl
            && instrumentId == runtimeInstrument.Descriptor.Id
                ? runtimeInstrument.Commands.SingleOrDefault(
                    candidate => candidate.Descriptor.Path == commandPath)
                : null;
        RfLabCommandMapping? mapping = command is null
            ? null
            : RfLabCommandMapping.All.SingleOrDefault(
                candidate => candidate.CommandPath == commandPath);
        if (command is null || mapping is null)
        {
            throw new KeyNotFoundException(
                "The requested RF-Lab runtime Command is not supported.");
        }

        if (argument is not null)
        {
            throw new ArgumentException(
                "RF-Lab Commands do not accept an argument.",
                nameof(argument));
        }

        switch (mapping.Kind)
        {
            case RfLabCommandKind.ApplyCarrier:
                await sessionAdapter.ApplyCarrierAsync(
                    GetTarget(RfLabTargetMapping.Frequency),
                    (int)GetTarget(RfLabTargetMapping.Attenuation),
                    cancellationToken).ConfigureAwait(false);
                break;

            case RfLabCommandKind.ApplyAmplitudeModulation:
                await sessionAdapter.ApplyAmplitudeModulationAsync(
                    GetTarget(RfLabTargetMapping.Frequency),
                    (int)GetTarget(RfLabTargetMapping.Attenuation),
                    GetTarget(RfLabTargetMapping.ModulationFrequency),
                    (int)GetTarget(RfLabTargetMapping.AmplitudeModulationDepth),
                    cancellationToken).ConfigureAwait(false);
                break;

            case RfLabCommandKind.ApplyFrequencyModulation:
                await sessionAdapter.ApplyFrequencyModulationAsync(
                    GetTarget(RfLabTargetMapping.Frequency),
                    (int)GetTarget(RfLabTargetMapping.Attenuation),
                    GetTarget(RfLabTargetMapping.ModulationFrequency),
                    GetTarget(RfLabTargetMapping.FrequencyModulationDeviation),
                    cancellationToken).ConfigureAwait(false);
                break;

            case RfLabCommandKind.StartSweep:
                await sessionAdapter.ApplySweepAsync(
                    GetTarget(RfLabTargetMapping.SweepStartFrequency),
                    GetTarget(RfLabTargetMapping.SweepStopFrequency),
                    (int)GetTarget(RfLabTargetMapping.SweepTime),
                    (int)GetTarget(RfLabTargetMapping.Attenuation),
                    mapping.SweepMode!.Value,
                    cancellationToken).ConfigureAwait(false);
                break;

            case RfLabCommandKind.ApplyClock:
                RfLabTargetMapping clockTarget = RfLabTargetMapping.All.Single(
                    candidate => candidate.ClockChannel == mapping.ClockChannel);
                await sessionAdapter.ApplyClockAsync(
                    mapping.ClockChannel!.Value,
                    GetTarget(clockTarget),
                    cancellationToken).ConfigureAwait(false);
                break;

            case RfLabCommandKind.IndicatorControl:
                RfLabIndicatorObservation observation = await sessionAdapter
                    .SetIndicatorAsync(mapping.IndicatorEnable!.Value, cancellationToken)
                    .ConfigureAwait(false);
                properties[RfLabProperties.IndicatorEnabled].UpdateValue(
                    CreateValue(observation.Enabled, observation.TimestampUtc));
                break;

            default:
                throw new KeyNotFoundException(
                    "The requested RF-Lab runtime Command is not supported.");
        }

        return command;
    }

    public ValueTask DisposeAsync() => sessionAdapter.DisposeAsync();

    private uint GetTarget(RfLabTargetMapping mapping)
    {
        lock (targetLock)
        {
            return (uint)Math.Round(stagedTargets[mapping.PropertyId]);
        }
    }

    private static bool TryFindTarget(
        PropertyId propertyId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out RfLabTargetMapping? mapping)
    {
        mapping = RfLabTargetMapping.All.SingleOrDefault(
            candidate => candidate.PropertyId == propertyId);
        return mapping is not null;
    }

    private static (RuntimeInstrument Instrument, bool SupportsControl)
        ValidateEndpoint(RuntimeEndpoint endpoint)
    {
        RuntimeInstrument instrument = endpoint.Instruments.Count == 1
            ? endpoint.Instruments[0]
            : throw Incompatible();

        if (IsCompatible(
                instrument,
                RfLabReadOnlyDefinition.EndpointDefinition.Instruments.Single()))
        {
            return (instrument, false);
        }

        if (IsCompatible(
                instrument,
                RfLabControlledSignalDefinition.EndpointDefinition.Instruments.Single()))
        {
            return (instrument, true);
        }

        throw Incompatible();
    }

    private static bool IsCompatible(
        RuntimeInstrument instrument,
        Core.Domain.Instruments.InstrumentDescriptor expected)
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

        for (int index = 0; index < instrument.Commands.Count; index++)
        {
            var actual = instrument.Commands[index].Descriptor;
            var required = expected.Interface.Commands[index];
            if (actual.Path != required.Path
                || (actual.Argument is null) != (required.Argument is null))
            {
                return false;
            }
        }

        return true;
    }

    private static double NormalizeRequestedValue(object? requestedValue)
    {
        return requestedValue switch
        {
            byte value => value,
            sbyte value => value,
            short value => value,
            ushort value => value,
            int value => value,
            uint value => value,
            long value => value,
            ulong value => value,
            decimal value => (double)value,
            float value when float.IsFinite(value) => value,
            double value when double.IsFinite(value) => value,
            float => throw new ArgumentException(
                "The requested RF-Lab target must be finite.",
                nameof(requestedValue)),
            double => throw new ArgumentException(
                "The requested RF-Lab target must be finite.",
                nameof(requestedValue)),
            _ => throw new ArgumentException(
                "The requested RF-Lab target must be numeric.",
                nameof(requestedValue))
        };
    }

    private static PropertyValue CreateValue(object value, DateTimeOffset timestampUtc) =>
        new(value, timestampUtc, PropertyQuality.Good);

    private static InvalidDataException Incompatible() =>
        new("The staged runtime endpoint is not compatible with a supported RF-Lab definition.");
}
