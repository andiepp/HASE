using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.App.Hosting;

public sealed class DesktopRuntimeHostKel103ConnectionDefinition
    : IEndpointConnectionDefinition
{
    public DesktopRuntimeHostKel103ConnectionDefinition(
        EndpointId expectedEndpointId,
        SerialTransportOptions serialOptions)
        : this(
            expectedEndpointId,
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition,
            serialOptions)
    {
    }

    public DesktopRuntimeHostKel103ConnectionDefinition(
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
        $"Configured KEL-103 connection for '{ExpectedEndpointId.Value}'";
}

public sealed class DesktopRuntimeHostKel103AttachmentService
    : IEndpointAttachmentService
{
    private readonly IDesktopRuntimeHostKel103AttachmentFactory attachmentFactory;

    public DesktopRuntimeHostKel103AttachmentService(
        IDesktopRuntimeHostKel103AttachmentFactory attachmentFactory)
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
            is not DesktopRuntimeHostKel103ConnectionDefinition connection)
        {
            throw new NotSupportedException(
                "The KEL-103 attachment service requires a KEL-103 connection definition.");
        }

        if (!ReferenceEquals(request.DescriptorSource, HostRepositoryDescriptorSource.Instance))
        {
            throw new ArgumentException(
                "A KEL-103 attachment requires the host repository descriptor source.",
                nameof(request));
        }

        IDesktopRuntimeHostKel103Attachment? attachment = null;

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
                    "The KEL-103 attachment factory returned null.");
            }

            if (attachment.RuntimeEndpoint.Descriptor.Id != connection.ExpectedEndpointId)
            {
                throw new InvalidOperationException(
                    "The KEL-103 attachment identity does not match the configured identity.");
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
                    "KEL-103 attachment was cancelled.",
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
                    "The configured KEL-103 endpoint could not be attached.");
            }

            if (!cleanupFailed)
            {
                throw sanitizedPrimary;
            }

            throw new AggregateException(
                "KEL-103 attachment and cleanup both failed.",
                sanitizedPrimary,
                new InvalidOperationException(
                    "The incomplete KEL-103 attachment did not shut down cleanly."));
        }
    }
}
