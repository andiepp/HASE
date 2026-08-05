using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Scpi.Kel103.Hosting;

/// <summary>
/// Owns KEL-103 recovery supervision and its published attachment.
/// </summary>
public sealed class Kel103SupervisedAttachment : IAsyncDisposable
{
    private readonly Kel103PublishedAttachment publishedAttachment;
    private readonly EndpointConnectionSupervisionLifetime supervisionLifetime;
    private readonly Kel103PublishedAttachmentSupervisor supervisor;
    private readonly object disposalLock = new();
    private Task? disposalTask;

    internal Kel103SupervisedAttachment(
        Kel103PublishedAttachment publishedAttachment,
        EndpointConnectionSupervisionLifetime supervisionLifetime,
        Kel103PublishedAttachmentSupervisor supervisor)
    {
        this.publishedAttachment = publishedAttachment
            ?? throw new ArgumentNullException(nameof(publishedAttachment));
        this.supervisionLifetime = supervisionLifetime
            ?? throw new ArgumentNullException(nameof(supervisionLifetime));
        this.supervisor = supervisor
            ?? throw new ArgumentNullException(nameof(supervisor));
    }

    public RuntimeEndpoint RuntimeEndpoint => publishedAttachment.RuntimeEndpoint;

    public IEndpointAttachmentPropertyOperations PropertyOperations =>
        publishedAttachment.PropertyOperations;

    public IEndpointAttachmentCommandOperations CommandOperations =>
        publishedAttachment.CommandOperations;

    public RuntimeEndpointConnectionStatistics GetConnectionStatistics() =>
        supervisor.GetStatistics();

    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
        {
            disposalTask ??= DisposeCoreAsync();
            return new ValueTask(disposalTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        List<Exception>? failures = null;

        try
        {
            await supervisionLifetime.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        try
        {
            await publishedAttachment.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        if (failures is { Count: 1 })
        {
            throw failures[0];
        }

        if (failures is { Count: > 1 })
        {
            throw new AggregateException(
                "The supervised KEL-103 attachment did not shut down cleanly.",
                failures);
        }
    }
}
