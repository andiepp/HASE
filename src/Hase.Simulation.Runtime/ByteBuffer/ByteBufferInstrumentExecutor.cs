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
                != ByteBufferDescriptorFactory.ReplaceCommandPath
            || argument is not ByteArrayValue replacement)
        {
            return Task.FromResult(
                new ExecutionResult<object?>(
                    success: false,
                    value: null));
        }

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
