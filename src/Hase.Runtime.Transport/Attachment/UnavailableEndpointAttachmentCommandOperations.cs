using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Provides a safe unavailable Command port for non-production operational
/// resource implementations that do not own an endpoint coordinator.
/// </summary>
internal sealed class UnavailableEndpointAttachmentCommandOperations
    : IEndpointAttachmentCommandOperations
{
    private UnavailableEndpointAttachmentCommandOperations()
    {
    }

    internal static UnavailableEndpointAttachmentCommandOperations Instance
    {
        get;
    } =
        new();

    /// <inheritdoc />
    public Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
        InstrumentId instrumentId,
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            commandPath);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.Unavailable,
                "The operational resource does not own an active Command port."));
    }
}