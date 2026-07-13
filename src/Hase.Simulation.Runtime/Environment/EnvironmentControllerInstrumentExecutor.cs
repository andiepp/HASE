using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Execution;
using Hase.Simulation.Environment;

namespace Hase.Simulation.Runtime.Environment;

/// <summary>
/// Executes engineering operations against an authoritative
/// simulated environment controller.
/// </summary>
public sealed class EnvironmentControllerInstrumentExecutor
    : IInstrumentExecutor
{
    private readonly EnvironmentControllerSimulation _simulation;

    public EnvironmentControllerInstrumentExecutor(
        EnvironmentControllerSimulation simulation)
    {
        _simulation = simulation
            ?? throw new ArgumentNullException(nameof(simulation));
    }

    public Task<ExecutionResult<PropertyValue?>> ReadPropertyAsync(
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        if (propertyId !=
            EnvironmentControllerDescriptorFactory
                .TargetTemperaturePropertyId)
        {
            return Task.FromResult(
                new ExecutionResult<PropertyValue?>(
                    success: false,
                    value: null));
        }

        var propertyValue =
            new PropertyValue(
                value:
                    _simulation.State.TargetTemperature,
                timestampUtc:
                    DateTimeOffset.UtcNow,
                quality:
                    PropertyQuality.Good);

        return Task.FromResult(
            new ExecutionResult<PropertyValue?>(
                success: true,
                value: propertyValue));
    }

    public Task<ExecutionResult> WritePropertyAsync(
        PropertyId propertyId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        if (propertyId !=
            EnvironmentControllerDescriptorFactory
                .TargetTemperaturePropertyId)
        {
            return Task.FromResult(
                ExecutionResult.Failed);
        }

        if (value is not double targetTemperature)
        {
            return Task.FromResult(
                ExecutionResult.Failed);
        }

        _simulation.SetTargetTemperature(
            targetTemperature);

        return Task.FromResult(
            ExecutionResult.Successful);
    }

    public Task<ExecutionResult<object?>> ExecuteCommandAsync(
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandPath);

        cancellationToken.ThrowIfCancellationRequested();

        if (commandPath !=
            EnvironmentControllerDescriptorFactory
                .ResetTargetTemperatureCommandPath)
        {
            return Task.FromResult(
                new ExecutionResult<object?>(
                    success: false,
                    value: null));
        }

        _simulation.ResetTargetTemperature();

        return Task.FromResult(
            new ExecutionResult<object?>(
                success: true,
                value: null));
    }
}