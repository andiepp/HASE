using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Mcnf.RfLab;
using Hase.Mcnf.RfLab.Hosting;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.App.Hosting;

public sealed class DesktopRuntimeHostRfLabConnectionDefinition
    : IEndpointConnectionDefinition
{
    public DesktopRuntimeHostRfLabConnectionDefinition(
        EndpointId expectedEndpointId,
        SerialTransportOptions serialOptions)
        : this(
            expectedEndpointId,
            RfLabReadOnlyDefinition.EndpointDefinition,
            serialOptions)
    {
    }

    public DesktopRuntimeHostRfLabConnectionDefinition(
        EndpointId expectedEndpointId,
        EndpointDescriptorDefinition definition,
        SerialTransportOptions serialOptions)
    {
        ExpectedEndpointId = expectedEndpointId
            ?? throw new ArgumentNullException(nameof(expectedEndpointId));
        Definition = definition
            ?? throw new ArgumentNullException(nameof(definition));
        SerialOptions = serialOptions
            ?? throw new ArgumentNullException(nameof(serialOptions));
    }

    public EndpointConnectionOrigin Origin => EndpointConnectionOrigin.Configured;
    public EndpointId ExpectedEndpointId { get; }
    public EndpointDescriptorDefinition Definition { get; }
    public SerialTransportOptions SerialOptions { get; }

    public override string ToString() =>
        $"Configured RF-Lab connection for '{ExpectedEndpointId.Value}'";
}

public interface IDesktopRuntimeHostRfLabAttachmentFactory
{
    Task<IDesktopRuntimeHostRfLabAttachment> OpenAsync(
        EndpointId endpointId,
        EndpointDescriptorDefinition definition,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default);
}

public interface IDesktopRuntimeHostRfLabAttachment : IAsyncDisposable
{
    RuntimeEndpoint RuntimeEndpoint { get; }
    IEndpointAttachmentPropertyOperations PropertyOperations { get; }
    IEndpointAttachmentCommandOperations CommandOperations { get; }
}

public sealed class DesktopRuntimeHostRfLabAttachmentFactory
    : IDesktopRuntimeHostRfLabAttachmentFactory
{
    private readonly RfLabSupervisedAttachmentFactory factory;

    public DesktopRuntimeHostRfLabAttachmentFactory(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        factory = new RfLabSupervisedAttachmentFactory(
            runtimeContext,
            serialByteStreamFactory);
    }

    public async Task<IDesktopRuntimeHostRfLabAttachment> OpenAsync(
        EndpointId endpointId,
        EndpointDescriptorDefinition definition,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        RfLabSupervisedAttachment attachment = await factory.OpenAsync(
                endpointId,
                definition,
                serialOptions,
                cancellationToken)
            .ConfigureAwait(false);

        return new DesktopRuntimeHostRfLabAttachment(attachment);
    }

    private sealed class DesktopRuntimeHostRfLabAttachment(
        RfLabSupervisedAttachment attachment)
        : IDesktopRuntimeHostRfLabAttachment
    {
        public RuntimeEndpoint RuntimeEndpoint => attachment.RuntimeEndpoint;

        public IEndpointAttachmentPropertyOperations PropertyOperations =>
            attachment.PropertyOperations;

        public IEndpointAttachmentCommandOperations CommandOperations =>
            attachment.CommandOperations;

        public ValueTask DisposeAsync() => attachment.DisposeAsync();
    }
}

public sealed class DesktopRuntimeHostRfLabAttachmentService
    : IEndpointAttachmentService
{
    private readonly IDesktopRuntimeHostRfLabAttachmentFactory attachmentFactory;

    public DesktopRuntimeHostRfLabAttachmentService(
        IDesktopRuntimeHostRfLabAttachmentFactory attachmentFactory)
    {
        this.attachmentFactory = attachmentFactory
            ?? throw new ArgumentNullException(nameof(attachmentFactory));
    }

    public async Task<IEndpointAttachmentSession> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ConnectionDefinition
            is not DesktopRuntimeHostRfLabConnectionDefinition connection)
        {
            throw new NotSupportedException(
                "The RF-Lab attachment service requires an RF-Lab connection definition.");
        }

        if (!ReferenceEquals(request.DescriptorSource, HostRepositoryDescriptorSource.Instance))
        {
            throw new ArgumentException(
                "An RF-Lab attachment requires the host repository descriptor source.",
                nameof(request));
        }

        IDesktopRuntimeHostRfLabAttachment? attachment = null;

        try
        {
            attachment = await attachmentFactory.OpenAsync(
                    connection.ExpectedEndpointId,
                    connection.Definition,
                    connection.SerialOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (attachment is null)
            {
                throw new InvalidOperationException(
                    "The RF-Lab attachment factory returned null.");
            }

            if (attachment.RuntimeEndpoint.Descriptor.Id != connection.ExpectedEndpointId)
            {
                throw new InvalidOperationException(
                    "The RF-Lab attachment identity does not match the configured identity.");
            }

            var session = new EndpointAttachmentSession(
                request,
                attachment.RuntimeEndpoint,
                attachment.PropertyOperations,
                attachment.CommandOperations,
                [attachment]);
            attachment = null;
            return session;
        }
        catch (Exception primaryFailure)
        {
            bool cleanupFailed = false;

            if (attachment is not null)
            {
                try
                {
                    await attachment.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    cleanupFailed = true;
                }
            }

            Exception sanitizedPrimary;
            if (primaryFailure is OperationCanceledException)
            {
                sanitizedPrimary = new OperationCanceledException(
                    "RF-Lab attachment was cancelled.",
                    cancellationToken);
            }
            else if (DesktopRuntimeHostEndpointStartupCoordinator
                .TryClassifyUnavailableFailure(
                    primaryFailure,
                    out string failureCategory))
            {
                sanitizedPrimary =
                    new DesktopRuntimeHostEndpointUnavailableException(
                        failureCategory);
            }
            else
            {
                sanitizedPrimary = new InvalidOperationException(
                    "The configured RF-Lab endpoint could not be attached.");
            }

            if (!cleanupFailed)
            {
                throw sanitizedPrimary;
            }

            throw new AggregateException(
                "RF-Lab attachment and cleanup both failed.",
                sanitizedPrimary,
                new InvalidOperationException(
                    "The incomplete RF-Lab attachment did not shut down cleanly."));
        }
    }
}
