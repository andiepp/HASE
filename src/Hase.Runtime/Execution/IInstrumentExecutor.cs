using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Execution;

/// <summary>
/// Executes engineering operations for one runtime instrument.
/// </summary>
public interface IInstrumentExecutor
{
    Task<ExecutionResult<PropertyValue?>> ReadPropertyAsync(
        PropertyId propertyId,
        CancellationToken cancellationToken = default);

    Task<ExecutionResult> WritePropertyAsync(
        PropertyId propertyId,
        object? value,
        CancellationToken cancellationToken = default);

    Task<ExecutionResult<object?>> ExecuteCommandAsync(
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default);
}