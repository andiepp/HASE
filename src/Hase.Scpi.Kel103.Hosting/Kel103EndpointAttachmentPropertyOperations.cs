using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103.Runtime;

namespace Hase.Scpi.Kel103.Hosting;

public sealed class Kel103EndpointAttachmentPropertyOperations
    : IEndpointAttachmentPropertyOperations
{
    private readonly Func<InstrumentId, PropertyId, CancellationToken, Task<RuntimeProperty>> readAsync;

    public Kel103EndpointAttachmentPropertyOperations(Kel103RuntimeEndpointAdapter runtimeAdapter)
        : this((runtimeAdapter ?? throw new ArgumentNullException(nameof(runtimeAdapter))).ReadAsync)
    {
    }

    internal Kel103EndpointAttachmentPropertyOperations(
        Func<InstrumentId, PropertyId, CancellationToken, Task<RuntimeProperty>> readAsync)
    {
        this.readAsync = readAsync ?? throw new ArgumentNullException(nameof(readAsync));
    }

    public async Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            RuntimeProperty property = await readAsync(
                instrumentId,
                propertyId,
                cancellationToken).ConfigureAwait(false);

            return property.CurrentValue is null
                ? Failure()
                : EndpointAttachmentPropertyOperationResult.Successful(property.CurrentValue);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            return NotSupported();
        }
        catch (TimeoutException)
        {
            return TimedOut();
        }
        catch (InvalidDataException)
        {
            return Failure();
        }
        catch (InvalidOperationException)
        {
            return Unavailable();
        }
        catch (IOException)
        {
            return Unavailable();
        }
    }

    public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(NotSupported());
    }

    private static EndpointAttachmentPropertyOperationResult NotSupported() =>
        EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.NotSupported);

    private static EndpointAttachmentPropertyOperationResult Failure() =>
        EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.Failure);

    private static EndpointAttachmentPropertyOperationResult TimedOut() =>
        EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.TimedOut,
            "The KEL-103 Property read timed out.");

    private static EndpointAttachmentPropertyOperationResult Unavailable() =>
        EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            "The KEL-103 attachment cannot currently perform the Property read.");
}
