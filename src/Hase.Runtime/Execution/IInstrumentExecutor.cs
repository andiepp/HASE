using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Execution;

/// <summary>
/// Executes engineering operations for one runtime instrument.
/// </summary>
public interface IInstrumentExecutor
{
    Task<ProtocolExecutionResult<PropertyValue?>> ReadPropertyAsync(
        PropertyId propertyId,
        CancellationToken cancellationToken = default);

    Task<ProtocolExecutionResult<PropertyValue?>> WritePropertyAsync(
        PropertyId propertyId,
        object? value,
        CancellationToken cancellationToken = default);
}