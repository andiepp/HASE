using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Execution;
using Hase.Runtime.Runtime;

namespace Hase.Simulation.Runtime.ByteBuffer;

/// <summary>
/// Executes deterministic operations against one authoritative simulated
/// byte buffer and its runtime Property cache.
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

        if (runtimeInstrument.FindProperty(
                ByteBufferDescriptorFactory.ValuePropertyId)
            is null)
        {
            throw new ArgumentException(
                "The runtime instrument does not contain the required "
                + "ByteArray buffer Property.",
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

        if (propertyId
            != ByteBufferDescriptorFactory.ValuePropertyId)
        {
            return Task.FromResult(
                new ExecutionResult<PropertyValue?>(
                    success: false,
                    value: null));
        }

        lock (gate)
        {
            return Task.FromResult(
                new ExecutionResult<PropertyValue?>(
                    success: true,
                    value:
                        CreatePropertyValue(
                            simulation.Value)));
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

        return Task.FromResult(
            ExecutionResult.Failed);
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
            PropertyValue propertyValue =
                CreatePropertyValue(
                    replacement);

            simulation.Replace(
                replacement);

            if (!runtimeInstrument.UpdatePropertyValue(
                    ByteBufferDescriptorFactory.ValuePropertyPath,
                    propertyValue))
            {
                throw new InvalidOperationException(
                    "The required ByteArray buffer Property could not be "
                    + "updated.");
            }

            return Task.FromResult(
                new ExecutionResult<object?>(
                    success: true,
                    value:
                        replacement));
        }
    }

    private PropertyValue CreatePropertyValue(
        ByteArrayValue value)
    {
        return new PropertyValue(
            value,
            timeProvider.GetUtcNow(),
            PropertyQuality.Good);
    }
}
