using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Runtime;
using Hase.Scpi.Kel103.Hosting;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.App.Hosting;

public interface IDesktopRuntimeHostKel103AttachmentFactory
{
    Task<IAsyncDisposable> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default);
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

    public async Task<IAsyncDisposable> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default) =>
        await factory.OpenAsync(
                endpointId,
                serialOptions,
                cancellationToken)
            .ConfigureAwait(false);
}

public sealed class DesktopRuntimeHostKel103AttachmentSet : IAsyncDisposable
{
    private readonly IReadOnlyList<IAsyncDisposable> attachments;
    private readonly object disposalLock = new();
    private Task? disposalTask;

    private DesktopRuntimeHostKel103AttachmentSet(
        IReadOnlyList<IAsyncDisposable> attachments)
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

        var opened = new List<IAsyncDisposable>(profiles.Count);

        try
        {
            for (int index = 0; index < profiles.Count; index++)
            {
                DesktopRuntimeHostKel103SerialEndpointProfile profile = profiles[index];
                var serialOptions = new SerialTransportOptions(
                    profile.SerialPort,
                    profile.BaudRate);
                IAsyncDisposable attachment = await attachmentFactory
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
        IReadOnlyList<IAsyncDisposable> ownedAttachments)
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
