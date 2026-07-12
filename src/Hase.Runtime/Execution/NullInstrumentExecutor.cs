using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Execution;

/// <summary>
/// Default executor used until a real executor is connected.
/// All execution operations are unsupported and therefore fail.
/// </summary>
public sealed class NullInstrumentExecutor
    : IInstrumentExecutor
{
    public Task<ExecutionResult<PropertyValue?>> ReadPropertyAsync(
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new ExecutionResult<PropertyValue?>(
                success: false,
                value: null));
    }

    public Task<ExecutionResult> WritePropertyAsync(
        PropertyId propertyId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            ExecutionResult.Failed);
    }
}