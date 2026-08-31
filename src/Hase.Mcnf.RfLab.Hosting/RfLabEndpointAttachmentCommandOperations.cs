using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Mcnf.RfLab.Runtime;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Mcnf.RfLab.Hosting;

public sealed class RfLabEndpointAttachmentCommandOperations
    : IEndpointAttachmentCommandOperations
{
    private readonly Func<InstrumentId, DescriptorPath, object?, CancellationToken, Task<RuntimeCommand>>
        executeAsync;
    private readonly Func<bool> isSessionFaulted;
    private readonly RuntimeEndpoint? runtimeEndpoint;
    private readonly TimeProvider timeProvider;

    public RfLabEndpointAttachmentCommandOperations(
        RfLabRuntimeEndpointAdapter runtimeAdapter,
        TimeProvider? timeProvider = null)
        : this(
            (runtimeAdapter ?? throw new ArgumentNullException(nameof(runtimeAdapter))).ExecuteAsync,
            () => runtimeAdapter.IsFaulted,
            runtimeAdapter.RuntimeEndpoint,
            timeProvider ?? TimeProvider.System)
    {
    }

    internal RfLabEndpointAttachmentCommandOperations(
        Func<InstrumentId, DescriptorPath, object?, CancellationToken, Task<RuntimeCommand>> executeAsync,
        Func<bool> isSessionFaulted,
        RuntimeEndpoint? runtimeEndpoint,
        TimeProvider timeProvider)
    {
        this.executeAsync = executeAsync
            ?? throw new ArgumentNullException(nameof(executeAsync));
        this.isSessionFaulted = isSessionFaulted
            ?? throw new ArgumentNullException(nameof(isSessionFaulted));
        this.runtimeEndpoint = runtimeEndpoint;
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
        InstrumentId instrumentId,
        DescriptorPath commandPath,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentId);
        ArgumentNullException.ThrowIfNull(commandPath);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await executeAsync(
                instrumentId,
                commandPath,
                argument,
                cancellationToken).ConfigureAwait(false);
            return EndpointAttachmentCommandOperationResult.Successful();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ProjectSessionFault();
            throw;
        }
        catch (KeyNotFoundException)
        {
            return Failure();
        }
        catch (ArgumentException)
        {
            return ArgumentNotSupported();
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
        catch (McnfDeviceErrorException exception)
        {
            // The node completed the exchange and rejected the Command; the
            // session remains healthy.
            return EndpointAttachmentCommandOperationResult.Failed(
                EndpointAttachmentCommandOperationStatus.Rejected,
                $"The RF-Lab node rejected the Command: {RfLabDeviceErrorCode.Describe(exception.ErrorCode)}.");
        }
        catch (InvalidOperationException)
        {
            if (!isSessionFaulted())
            {
                return Rejected();
            }

            ProjectSessionFault();
            return Unavailable();
        }
        catch (RfLabMutationOutcomeUncertainException)
        {
            ProjectSessionFault();
            return Uncertain();
        }
        catch (McnfExchangeException exception)
            when (exception.ExecutionMayHaveOccurred)
        {
            ProjectSessionFault();
            return Uncertain();
        }
        catch (IOException)
        {
            ProjectSessionFault();
            return Unavailable();
        }
    }

    private static EndpointAttachmentCommandOperationResult ArgumentNotSupported() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.ArgumentNotSupported);

    private static EndpointAttachmentCommandOperationResult Rejected() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Rejected,
            "The RF-Lab Command was rejected.");

    private static EndpointAttachmentCommandOperationResult Failure() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Failure);

    private static EndpointAttachmentCommandOperationResult TimedOut() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.TimedOut,
            "The RF-Lab Command operation timed out.");

    private static EndpointAttachmentCommandOperationResult Unavailable() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            "The RF-Lab attachment cannot currently perform the Command operation.");

    private static EndpointAttachmentCommandOperationResult Uncertain() =>
        EndpointAttachmentCommandOperationResult.Failed(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            "The RF-Lab Command outcome is uncertain. Physically verify the RF output state before continuing.");

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
                "The RF-Lab communication session is faulted."));
    }
}
