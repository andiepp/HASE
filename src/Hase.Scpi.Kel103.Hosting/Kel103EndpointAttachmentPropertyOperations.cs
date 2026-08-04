using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103.Runtime;

namespace Hase.Scpi.Kel103.Hosting;

public sealed class Kel103EndpointAttachmentPropertyOperations
    : IEndpointAttachmentPropertyOperations
{
    private readonly Func<InstrumentId, PropertyId, CancellationToken, Task<RuntimeProperty>> readAsync;
    private readonly Func<InstrumentId, PropertyId, object?, CancellationToken, Task<RuntimeProperty>>? writeAsync;
    private readonly Func<bool> isSessionFaulted;
    private readonly RuntimeEndpoint? runtimeEndpoint;
    private readonly TimeProvider timeProvider;

    public Kel103EndpointAttachmentPropertyOperations(
        Kel103RuntimeEndpointAdapter runtimeAdapter,
        TimeProvider? timeProvider = null)
        : this(
            (runtimeAdapter ?? throw new ArgumentNullException(nameof(runtimeAdapter))).ReadAsync,
            runtimeAdapter.WriteAsync,
            () => runtimeAdapter.IsFaulted,
            runtimeAdapter.RuntimeEndpoint,
            timeProvider ?? TimeProvider.System)
    {
    }

    internal Kel103EndpointAttachmentPropertyOperations(
        Func<InstrumentId, PropertyId, CancellationToken, Task<RuntimeProperty>> readAsync)
    {
        this.readAsync = readAsync ?? throw new ArgumentNullException(nameof(readAsync));
        writeAsync = null;
        isSessionFaulted = static () => false;
        runtimeEndpoint = null;
        timeProvider = TimeProvider.System;
    }

    internal Kel103EndpointAttachmentPropertyOperations(
        Func<InstrumentId, PropertyId, CancellationToken, Task<RuntimeProperty>> readAsync,
        Func<bool> isSessionFaulted,
        RuntimeEndpoint? runtimeEndpoint,
        TimeProvider timeProvider)
    {
        this.readAsync = readAsync ?? throw new ArgumentNullException(nameof(readAsync));
        writeAsync = null;
        this.isSessionFaulted = isSessionFaulted
            ?? throw new ArgumentNullException(nameof(isSessionFaulted));
        this.runtimeEndpoint = runtimeEndpoint;
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    internal Kel103EndpointAttachmentPropertyOperations(
        Func<InstrumentId, PropertyId, CancellationToken, Task<RuntimeProperty>> readAsync,
        Func<InstrumentId, PropertyId, object?, CancellationToken, Task<RuntimeProperty>> writeAsync,
        Func<bool> isSessionFaulted,
        RuntimeEndpoint? runtimeEndpoint,
        TimeProvider timeProvider)
    {
        this.readAsync = readAsync ?? throw new ArgumentNullException(nameof(readAsync));
        this.writeAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
        this.isSessionFaulted = isSessionFaulted
            ?? throw new ArgumentNullException(nameof(isSessionFaulted));
        this.runtimeEndpoint = runtimeEndpoint;
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
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
            ProjectSessionFault();
            throw;
        }
        catch (KeyNotFoundException)
        {
            return NotSupported();
        }
        catch (TimeoutException)
        {
            ProjectSessionFault();
            return TimedOut();
        }
        catch (InvalidDataException)
        {
            ProjectSessionFault();
            return Failure();
        }
        catch (InvalidOperationException)
        {
            ProjectSessionFault();
            return Unavailable();
        }
        catch (IOException)
        {
            ProjectSessionFault();
            return Unavailable();
        }
    }

    public async Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(propertyId);
        cancellationToken.ThrowIfCancellationRequested();
        if (writeAsync is null)
        {
            return NotSupported();
        }

        try
        {
            RuntimeProperty property = await writeAsync(
                instrumentId,
                propertyId,
                requestedValue,
                cancellationToken).ConfigureAwait(false);

            return property.CurrentValue is null
                ? Failure()
                : EndpointAttachmentPropertyOperationResult.Successful(property.CurrentValue);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ProjectSessionFault();
            throw;
        }
        catch (KeyNotFoundException)
        {
            return NotSupported();
        }
        catch (ArgumentException)
        {
            return Failure();
        }
        catch (TimeoutException)
        {
            ProjectSessionFault();
            return WriteTimedOut();
        }
        catch (InvalidDataException)
        {
            ProjectSessionFault();
            return Failure();
        }
        catch (InvalidOperationException)
        {
            ProjectSessionFault();
            return WriteUnavailable();
        }
        catch (IOException)
        {
            ProjectSessionFault();
            return WriteUnavailable();
        }
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

    private static EndpointAttachmentPropertyOperationResult WriteTimedOut() =>
        EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.TimedOut,
            "The KEL-103 Property write timed out.");

    private static EndpointAttachmentPropertyOperationResult WriteUnavailable() =>
        EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            "The KEL-103 attachment cannot currently perform the Property write.");

    private void ProjectSessionFault()
    {
        if (runtimeEndpoint is null
            || !isSessionFaulted()
            || runtimeEndpoint.ConnectionStatus.State == EndpointConnectionState.Faulted)
        {
            return;
        }

        runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Faulted,
                timeProvider.GetUtcNow(),
                "The KEL-103 communication session is faulted."));
    }
}
