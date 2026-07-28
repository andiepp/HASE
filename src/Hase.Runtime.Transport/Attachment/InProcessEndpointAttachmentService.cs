using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Execution;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Attaches configured in-process endpoints through the normal runtime-host
/// attachment lifecycle.
/// </summary>
public sealed class InProcessEndpointAttachmentService
    : IEndpointAttachmentService
{
    private readonly RuntimeContext runtimeContext;

    public InProcessEndpointAttachmentService(
        RuntimeContext runtimeContext)
    {
        this.runtimeContext =
            runtimeContext
            ?? throw new ArgumentNullException(
                nameof(runtimeContext));
    }

    public async Task<IEndpointAttachmentSession> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ConnectionDefinition
            is not InProcessEndpointConnectionDefinition definition)
        {
            throw new ArgumentException(
                "The request does not contain an in-process endpoint "
                + "connection definition.",
                nameof(request));
        }

        if (request.DescriptorSource
            is not InProcessEndpointDescriptorSource)
        {
            throw new ArgumentException(
                "An in-process endpoint requires the in-process descriptor "
                + "source.",
                nameof(request));
        }

        RuntimeEndpoint runtimeEndpoint =
            runtimeContext.CreateEndpoint(
                definition.Descriptor);

        var operations =
            new InProcessEndpointOperations(
                runtimeEndpoint);

        try
        {
            foreach (
                RuntimeInstrument runtimeInstrument
                in runtimeEndpoint.Instruments)
            {
                runtimeInstrument.ConnectExecutor(
                    definition.CreateExecutor(
                        runtimeInstrument));

                foreach (
                    RuntimeProperty runtimeProperty
                    in runtimeInstrument.Properties)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ExecutionResult<PropertyValue?> initialRead =
                        await runtimeInstrument.Executor.ReadPropertyAsync(
                            runtimeProperty.Descriptor.Id,
                            cancellationToken);

                    if (!initialRead.Success
                        || initialRead.Value is null)
                    {
                        throw new InvalidOperationException(
                            "The in-process endpoint could not initially "
                            + "synchronize all Properties.");
                    }

                    runtimeInstrument.UpdatePropertyValue(
                        runtimeProperty.Descriptor.Path,
                        initialRead.Value);
                }
            }

            runtimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready));

            runtimeContext.PublishEndpoint(
                runtimeEndpoint);

            return new EndpointAttachmentSession(
                request,
                runtimeEndpoint,
                operations,
                operations,
                [
                    new PublishedRuntimeEndpointLifetime(
                        runtimeContext,
                        runtimeEndpoint)
                ]);
        }
        catch
        {
            runtimeContext.RemoveEndpoint(
                runtimeEndpoint);

            throw;
        }
    }

    private sealed class PublishedRuntimeEndpointLifetime
        : IAsyncDisposable
    {
        private RuntimeContext? runtimeContext;
        private RuntimeEndpoint? runtimeEndpoint;

        public PublishedRuntimeEndpointLifetime(
            RuntimeContext runtimeContext,
            RuntimeEndpoint runtimeEndpoint)
        {
            this.runtimeContext =
                runtimeContext;
            this.runtimeEndpoint =
                runtimeEndpoint;
        }

        public ValueTask DisposeAsync()
        {
            RuntimeContext? context =
                Interlocked.Exchange(
                    ref runtimeContext,
                    null);

            RuntimeEndpoint? endpoint =
                Interlocked.Exchange(
                    ref runtimeEndpoint,
                    null);

            if (context is not null
                && endpoint is not null)
            {
                endpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(
                        EndpointConnectionState.Disconnected));

                context.RemoveEndpoint(
                    endpoint);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class InProcessEndpointOperations
        : IEndpointAttachmentPropertyOperations,
          IEndpointAttachmentCommandOperations
    {
        private readonly RuntimeEndpoint runtimeEndpoint;

        public InProcessEndpointOperations(
            RuntimeEndpoint runtimeEndpoint)
        {
            this.runtimeEndpoint =
                runtimeEndpoint;
        }

        public async Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                instrumentId);
            ArgumentNullException.ThrowIfNull(
                propertyId);

            RuntimeInstrument? instrument =
                runtimeEndpoint.FindInstrument(
                    instrumentId);

            if (instrument is null
                || instrument.FindProperty(
                    propertyId)
                is null)
            {
                return EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.NotSupported);
            }

            ExecutionResult<PropertyValue?> execution =
                await instrument.Executor.ReadPropertyAsync(
                    propertyId,
                    cancellationToken);

            return execution.Success
                && execution.Value is not null
                ? EndpointAttachmentPropertyOperationResult.Successful(
                    execution.Value)
                : EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.Failure);
        }

        public async Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                instrumentId);
            ArgumentNullException.ThrowIfNull(
                propertyId);

            RuntimeInstrument? instrument =
                runtimeEndpoint.FindInstrument(
                    instrumentId);

            if (instrument is null
                || instrument.FindProperty(
                    propertyId)
                is null)
            {
                return EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.NotSupported);
            }

            ExecutionResult execution =
                await instrument.Executor.WritePropertyAsync(
                    propertyId,
                    requestedValue,
                    cancellationToken);

            if (!execution.Success)
            {
                return EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.Rejected);
            }

            ExecutionResult<PropertyValue?> confirmed =
                await instrument.Executor.ReadPropertyAsync(
                    propertyId,
                    cancellationToken);

            return confirmed.Success
                && confirmed.Value is not null
                ? EndpointAttachmentPropertyOperationResult.Successful(
                    confirmed.Value)
                : EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.Failure);
        }

        public async Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
            InstrumentId instrumentId,
            DescriptorPath commandPath,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                instrumentId);
            ArgumentNullException.ThrowIfNull(
                commandPath);

            RuntimeInstrument? instrument =
                runtimeEndpoint.FindInstrument(
                    instrumentId);

            if (instrument is null
                || instrument.FindCommand(
                    commandPath)
                is null)
            {
                return EndpointAttachmentCommandOperationResult.Failed(
                    EndpointAttachmentCommandOperationStatus.Rejected);
            }

            ExecutionResult<object?> execution =
                await instrument.Executor.ExecuteCommandAsync(
                    commandPath,
                    argument,
                    cancellationToken);

            return execution.Success
                ? EndpointAttachmentCommandOperationResult.Successful(
                    execution.Value)
                : EndpointAttachmentCommandOperationResult.Failed(
                    EndpointAttachmentCommandOperationStatus.Rejected);
        }
    }
}
