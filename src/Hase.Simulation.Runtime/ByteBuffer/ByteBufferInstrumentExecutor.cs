using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Execution;
using Hase.Runtime.Runtime;

namespace Hase.Simulation.Runtime.ByteBuffer;

/// <summary>
/// Executes deterministic multi-type Property operations and ByteArray
/// Command operations against one authoritative simulation.
/// </summary>
public sealed class ByteBufferInstrumentExecutor
    : IInstrumentExecutor
{
    private readonly object gate =
        new();
    private readonly ByteBufferSimulation simulation;
    private readonly RuntimeInstrument runtimeInstrument;
    private readonly TimeProvider timeProvider;

    public ByteBufferInstrumentExecutor(
        ByteBufferSimulation simulation,
        RuntimeInstrument runtimeInstrument,
        TimeProvider? timeProvider = null)
    {
        this.simulation =
            simulation
            ?? throw new ArgumentNullException(
                nameof(simulation));
        this.runtimeInstrument =
            runtimeInstrument
            ?? throw new ArgumentNullException(
                nameof(runtimeInstrument));
        this.timeProvider =
            timeProvider
            ?? TimeProvider.System;

        PropertyId[] requiredProperties =
        [
            ByteBufferDescriptorFactory.EnabledPropertyId,
            ByteBufferDescriptorFactory.SetpointPropertyId,
            ByteBufferDescriptorFactory.LabelPropertyId,
            ByteBufferDescriptorFactory.ValuePropertyId
        ];

        if (requiredProperties.Any(
                propertyId =>
                    runtimeInstrument.FindProperty(
                        propertyId)
                    is null))
        {
            throw new ArgumentException(
                "The runtime instrument does not contain all required "
                + "Property-editor validation Properties.",
                nameof(runtimeInstrument));
        }

        DescriptorPath[] requiredEvents =
        [
            ByteBufferDescriptorFactory.NoPayloadEventPath,
            ByteBufferDescriptorFactory.BooleanEventPath,
            ByteBufferDescriptorFactory.NumericEventPath,
            ByteBufferDescriptorFactory.StringEventPath,
            ByteBufferDescriptorFactory.ByteArrayEventPath
        ];

        if (requiredEvents.Any(
                path =>
                    runtimeInstrument.FindEvent(
                        path)
                    is null))
        {
            throw new ArgumentException(
                "The runtime instrument does not contain all required "
                + "Event-validation Events.",
                nameof(runtimeInstrument));
        }
    }

    public Task<ExecutionResult<PropertyValue?>> ReadPropertyAsync(
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            object? value =
                GetCurrentValue(
                    propertyId);

            return Task.FromResult(
                new ExecutionResult<PropertyValue?>(
                    success:
                        value is not null,
                    value:
                        value is null
                            ? null
                            : CreatePropertyValue(
                                value)));
        }
    }

    public Task<ExecutionResult> WritePropertyAsync(
        PropertyId propertyId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            DescriptorPath? path =
                null;

            if (propertyId
                    == ByteBufferDescriptorFactory.EnabledPropertyId
                && value is bool enabled)
            {
                simulation.SetEnabled(
                    enabled);
                path =
                    ByteBufferDescriptorFactory.EnabledPropertyPath;
            }
            else if (propertyId
                        == ByteBufferDescriptorFactory.SetpointPropertyId
                    && value is double setpoint
                    && simulation.TrySetSetpoint(
                        setpoint))
            {
                path =
                    ByteBufferDescriptorFactory.SetpointPropertyPath;
            }
            else if (propertyId
                        == ByteBufferDescriptorFactory.LabelPropertyId
                    && value is string label)
            {
                simulation.SetLabel(
                    label);
                path =
                    ByteBufferDescriptorFactory.LabelPropertyPath;
            }
            else if (propertyId
                        == ByteBufferDescriptorFactory.ValuePropertyId
                    && value is ByteArrayValue bytes)
            {
                simulation.Replace(
                    bytes);
                path =
                    ByteBufferDescriptorFactory.ValuePropertyPath;
            }

            if (path is null)
            {
                return Task.FromResult(
                    ExecutionResult.Failed);
            }

            UpdateRuntimeProperty(
                path,
                value!);

            return Task.FromResult(
                ExecutionResult.Successful);
        }
    }

    public Task<ExecutionResult<object?>> ExecuteCommandAsync(
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            commandPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (commandPath
                == ByteBufferDescriptorFactory.ReplaceCommandPath
            && argument is ByteArrayValue replacement)
        {
            lock (gate)
            {
                simulation.Replace(
                    replacement);
                UpdateRuntimeProperty(
                    ByteBufferDescriptorFactory.ValuePropertyPath,
                    replacement);

                return Task.FromResult(
                    new ExecutionResult<object?>(
                        success: true,
                        value:
                            replacement));
            }
        }

        if (argument is null
            && TryResolveEventOccurrence(
                commandPath,
                out DescriptorPath eventPath,
                out object? value))
        {
            RuntimeEvent runtimeEvent =
                runtimeInstrument.FindEvent(
                    eventPath)
                ?? throw new InvalidOperationException(
                    "A required Event-validation Event could not be found.");

            runtimeEvent.PublishOccurrence(
                timeProvider.GetUtcNow(),
                value);

            return Task.FromResult(
                new ExecutionResult<object?>(
                    success: true,
                    value: null));
        }

        return Task.FromResult(
            new ExecutionResult<object?>(
                success: false,
                value: null));
    }

    private static bool TryResolveEventOccurrence(
        DescriptorPath commandPath,
        out DescriptorPath eventPath,
        out object? value)
    {
        if (commandPath
            == ByteBufferDescriptorFactory.EmitNoPayloadCommandPath)
        {
            eventPath =
                ByteBufferDescriptorFactory.NoPayloadEventPath;
            value =
                null;
            return true;
        }

        if (commandPath
            == ByteBufferDescriptorFactory.EmitBooleanCommandPath)
        {
            eventPath =
                ByteBufferDescriptorFactory.BooleanEventPath;
            value =
                true;
            return true;
        }

        if (commandPath
            == ByteBufferDescriptorFactory.EmitNumericCommandPath)
        {
            eventPath =
                ByteBufferDescriptorFactory.NumericEventPath;
            value =
                23.5;
            return true;
        }

        if (commandPath
            == ByteBufferDescriptorFactory.EmitStringCommandPath)
        {
            eventPath =
                ByteBufferDescriptorFactory.StringEventPath;
            value =
                "HASE event validation";
            return true;
        }

        if (commandPath
            == ByteBufferDescriptorFactory.EmitByteArrayCommandPath)
        {
            eventPath =
                ByteBufferDescriptorFactory.ByteArrayEventPath;
            value =
                new ByteArrayValue(
                    new byte[]
                    {
                        0x01,
                        0xAB,
                        0x00,
                        0xFF
                    });
            return true;
        }

        eventPath =
            commandPath;
        value =
            null;
        return false;
    }

    private object? GetCurrentValue(
        PropertyId propertyId)
    {
        if (propertyId
            == ByteBufferDescriptorFactory.EnabledPropertyId)
        {
            return simulation.Enabled;
        }

        if (propertyId
            == ByteBufferDescriptorFactory.SetpointPropertyId)
        {
            return simulation.Setpoint;
        }

        if (propertyId
            == ByteBufferDescriptorFactory.LabelPropertyId)
        {
            return simulation.Label;
        }

        return propertyId
                == ByteBufferDescriptorFactory.ValuePropertyId
            ? simulation.Value
            : null;
    }

    private void UpdateRuntimeProperty(
        DescriptorPath path,
        object value)
    {
        if (!runtimeInstrument.UpdatePropertyValue(
                path,
                CreatePropertyValue(
                    value)))
        {
            throw new InvalidOperationException(
                "A required Property-editor validation Property could "
                + "not be updated.");
        }
    }

    private PropertyValue CreatePropertyValue(
        object value)
    {
        return new PropertyValue(
            value,
            timeProvider.GetUtcNow(),
            PropertyQuality.Good);
    }
}
