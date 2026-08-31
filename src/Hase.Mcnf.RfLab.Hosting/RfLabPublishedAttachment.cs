using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.Mcnf.RfLab.Hosting;

/// <summary>
/// Owns one ready, published RF-Lab endpoint and its operational connection.
/// </summary>
public sealed class RfLabPublishedAttachment : IAsyncDisposable
{
    private readonly RuntimeContext runtimeContext;
    private readonly RfLabPublishedConnectionSlot connectionSlot;
    private readonly object disposalLock = new();
    private Task? disposalTask;

    internal RfLabPublishedAttachment(
        RuntimeContext runtimeContext,
        RfLabPublishedConnectionSlot connectionSlot)
    {
        this.runtimeContext = runtimeContext
            ?? throw new ArgumentNullException(nameof(runtimeContext));
        this.connectionSlot = connectionSlot
            ?? throw new ArgumentNullException(nameof(connectionSlot));
    }

    public RuntimeEndpoint RuntimeEndpoint => connectionSlot.RuntimeEndpoint;

    public IEndpointAttachmentPropertyOperations PropertyOperations =>
        connectionSlot;

    public IEndpointAttachmentCommandOperations CommandOperations =>
        connectionSlot;

    internal Task ProbeHealthAsync(CancellationToken cancellationToken = default) =>
        connectionSlot.ProbeHealthAsync(cancellationToken);

    public Task ReplaceAsync(
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default) =>
        connectionSlot.ReplaceAsync(serialOptions, cancellationToken);

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
            await connectionSlot.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        try
        {
            RuntimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(EndpointConnectionState.Disconnected));
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        try
        {
            runtimeContext.RemoveEndpoint(RuntimeEndpoint);
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
                "The RF-Lab published attachment did not shut down cleanly.",
                failures);
        }
    }
}
