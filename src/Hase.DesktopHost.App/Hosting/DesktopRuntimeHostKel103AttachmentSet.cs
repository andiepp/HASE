using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103.Hosting;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.App.Hosting;

public interface IDesktopRuntimeHostKel103AttachmentFactory
{
    Task<IDesktopRuntimeHostKel103Attachment> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default);
}

public interface IDesktopRuntimeHostKel103Attachment : IAsyncDisposable
{
    RuntimeEndpoint RuntimeEndpoint { get; }
    IEndpointAttachmentPropertyOperations PropertyOperations { get; }
}

public sealed class DesktopRuntimeHostKel103AttachmentFactory
    : IDesktopRuntimeHostKel103AttachmentFactory
{
    private readonly Kel103SupervisedAttachmentFactory factory;

    public DesktopRuntimeHostKel103AttachmentFactory(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory)
    {
        factory = new Kel103SupervisedAttachmentFactory(
            runtimeContext,
            serialByteStreamFactory);
    }

    public async Task<IDesktopRuntimeHostKel103Attachment> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        Kel103SupervisedAttachment attachment = await factory.OpenAsync(
                endpointId,
                serialOptions,
                cancellationToken)
            .ConfigureAwait(false);

        return new DesktopRuntimeHostKel103Attachment(attachment);
    }

    private sealed class DesktopRuntimeHostKel103Attachment(
        Kel103SupervisedAttachment attachment)
        : IDesktopRuntimeHostKel103Attachment
    {
        public RuntimeEndpoint RuntimeEndpoint => attachment.RuntimeEndpoint;

        public IEndpointAttachmentPropertyOperations PropertyOperations =>
            attachment.PropertyOperations;

        public ValueTask DisposeAsync() => attachment.DisposeAsync();
    }
}

public sealed class DesktopRuntimeHostKel103AttachmentSet : IAsyncDisposable
{
    private readonly IReadOnlyList<IDesktopRuntimeHostKel103Attachment> attachments;
    private readonly object disposalLock = new();
    private Task? disposalTask;

    private DesktopRuntimeHostKel103AttachmentSet(
        IReadOnlyList<IDesktopRuntimeHostKel103Attachment> attachments)
    {
        this.attachments = attachments;
    }

    public int Count => attachments.Count;

    public static async Task<DesktopRuntimeHostKel103AttachmentSet> OpenAsync(
        IReadOnlyList<DesktopRuntimeHostKel103SerialEndpointProfile> profiles,
        IReadOnlyList<DesktopRuntimeHostKel103EndpointPlan> plans,
        IDesktopRuntimeHostKel103AttachmentFactory attachmentFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(attachmentFactory);
        cancellationToken.ThrowIfCancellationRequested();

        if (profiles.Count != plans.Count)
        {
            throw new ArgumentException(
                "KEL-103 profiles and preflight plans must have equal counts.",
                nameof(plans));
        }

        for (int index = 0; index < profiles.Count; index++)
        {
            if (new EndpointId(profiles[index].ExpectedEndpointId)
                != plans[index].ExpectedEndpointId)
            {
                throw new ArgumentException(
                    "A KEL-103 profile does not match its preflight plan.",
                    nameof(plans));
            }
        }

        var opened = new List<IDesktopRuntimeHostKel103Attachment>(profiles.Count);

        try
        {
            for (int index = 0; index < profiles.Count; index++)
            {
                DesktopRuntimeHostKel103SerialEndpointProfile profile = profiles[index];
                var serialOptions = new SerialTransportOptions(
                    profile.SerialPort,
                    profile.BaudRate);
                IDesktopRuntimeHostKel103Attachment attachment = await attachmentFactory
                    .OpenAsync(
                        plans[index].ExpectedEndpointId,
                        serialOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                opened.Add(
                    attachment
                    ?? throw new InvalidOperationException(
                        "The KEL-103 attachment factory returned null."));
            }

            return new DesktopRuntimeHostKel103AttachmentSet(opened.AsReadOnly());
        }
        catch (Exception primaryFailure)
        {
            IReadOnlyList<Exception> cleanupFailures =
                await DisposeReverseAsync(opened).ConfigureAwait(false);
            Exception sanitizedPrimary = SanitizePrimaryFailure(
                primaryFailure,
                cancellationToken);

            if (cleanupFailures.Count == 0)
            {
                throw sanitizedPrimary;
            }

            throw new AggregateException(
                "KEL-103 attachment-set creation and cleanup both failed.",
                new[] { sanitizedPrimary }.Concat(cleanupFailures));
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
        {
            disposalTask ??= DisposeCoreAsync();
            return new ValueTask(disposalTask);
        }
    }

    public override string ToString() =>
        $"Desktop Runtime Host KEL-103 attachment set ({Count} attachments)";

    private async Task DisposeCoreAsync()
    {
        IReadOnlyList<Exception> failures =
            await DisposeReverseAsync(attachments).ConfigureAwait(false);

        if (failures.Count == 1)
        {
            throw failures[0];
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(
                "The KEL-103 attachment set did not shut down cleanly.",
                failures);
        }
    }

    private static async Task<IReadOnlyList<Exception>> DisposeReverseAsync(
        IReadOnlyList<IDesktopRuntimeHostKel103Attachment> ownedAttachments)
    {
        var failures = new List<Exception>();

        for (int index = ownedAttachments.Count - 1; index >= 0; index--)
        {
            try
            {
                await ownedAttachments[index].DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                failures.Add(
                    new InvalidOperationException(
                        "A KEL-103 attachment did not shut down cleanly."));
            }
        }

        return failures.AsReadOnly();
    }

    private static Exception SanitizePrimaryFailure(
        Exception failure,
        CancellationToken cancellationToken) =>
        failure is OperationCanceledException
            ? new OperationCanceledException(
                "KEL-103 attachment-set creation was cancelled.",
                cancellationToken)
            : new InvalidOperationException(
                "A configured KEL-103 attachment could not be opened.");
}
