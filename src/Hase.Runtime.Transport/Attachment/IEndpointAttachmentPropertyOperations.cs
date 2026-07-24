using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Provides transport-independent Property operations bound to one endpoint
/// attachment.
/// </summary>
/// <remarks>
/// Implementations remain owned by their attachment session and address only
/// the runtime endpoint to which that session is bound.
/// </remarks>
public interface IEndpointAttachmentPropertyOperations
{
    /// <summary>
    /// Reads one Property authoritatively from the attached endpoint.
    /// </summary>
    Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one Property and returns its endpoint-confirmed value.
    /// </summary>
    Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        object? requestedValue,
        CancellationToken cancellationToken = default);
}