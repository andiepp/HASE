using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Execution;

/// <summary>
/// Default executor used until a real executor is attached.
/// </summary>
public sealed class NullInstrumentExecutor : IInstrumentExecutor
{
    public Task<ProtocolExecutionResult<PropertyValue?>> ReadPropertyAsync(
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(propertyId);

        return Task.FromResult(
            new ProtocolExecutionResult<PropertyValue?>(
                false,
                null));
    }

    public Task<ProtocolExecutionResult<PropertyValue?>> WritePropertyAsync(
        PropertyId propertyId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(propertyId);

        return Task.FromResult(
            new ProtocolExecutionResult<PropertyValue?>(
                false,
                null));
    }
}