using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Mcnf.RfLab.Hosting;

/// <summary>
/// Owns RF-Lab recovery supervision and its published attachment.
/// </summary>
public sealed class RfLabSupervisedAttachment : IAsyncDisposable
{
    private readonly RfLabPublishedAttachment publishedAttachment;
    private readonly EndpointConnectionSupervisionLifetime supervisionLifetime;
    private readonly EndpointConnectionSupervisionLifetime passiveHealthLifetime;
    private readonly RfLabPublishedAttachmentSupervisor supervisor;
    private readonly object disposalLock = new();
    private Task? disposalTask;

    internal RfLabSupervisedAttachment(
        RfLabPublishedAttachment publishedAttachment,
        EndpointConnectionSupervisionLifetime supervisionLifetime,
        EndpointConnectionSupervisionLifetime passiveHealthLifetime,
        RfLabPublishedAttachmentSupervisor supervisor)
    {
        this.publishedAttachment = publishedAttachment
            ?? throw new ArgumentNullException(nameof(publishedAttachment));
        this.supervisionLifetime = supervisionLifetime
            ?? throw new ArgumentNullException(nameof(supervisionLifetime));
        this.passiveHealthLifetime = passiveHealthLifetime
            ?? throw new ArgumentNullException(nameof(passiveHealthLifetime));
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
            await passiveHealthLifetime.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

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
                "The supervised RF-Lab attachment did not shut down cleanly.",
                failures);
        }
    }
}
