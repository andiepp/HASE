using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Provides transport-independent Command operations bound to one endpoint
/// attachment.
/// </summary>
/// <remarks>
/// Implementations remain owned by their attachment session and address only
/// the runtime endpoint to which that session is bound.
/// </remarks>
public interface IEndpointAttachmentCommandOperations
{
    /// <summary>
    /// Executes one logical Command through the attached endpoint.
    /// </summary>
    Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
        InstrumentId instrumentId,
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default);
}