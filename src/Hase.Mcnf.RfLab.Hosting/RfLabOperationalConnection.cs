using Hase.Mcnf.RfLab.Runtime;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Mcnf.RfLab.Hosting;

/// <summary>
/// Owns one synchronized, staged RF-Lab runtime endpoint and its serial
/// MCNF session.
/// </summary>
public sealed class RfLabOperationalConnection : IAsyncDisposable
{
    private readonly RfLabRuntimeEndpointAdapter runtimeAdapter;
    private readonly object disposalLock = new();
    private Task? disposalTask;

    internal RfLabOperationalConnection(
        RfLabRuntimeEndpointAdapter runtimeAdapter,
        RfLabEndpointAttachmentPropertyOperations propertyOperations,
        RfLabEndpointAttachmentCommandOperations commandOperations)
    {
        this.runtimeAdapter = runtimeAdapter
            ?? throw new ArgumentNullException(nameof(runtimeAdapter));
        PropertyOperations = propertyOperations
            ?? throw new ArgumentNullException(nameof(propertyOperations));
        CommandOperations = commandOperations
            ?? throw new ArgumentNullException(nameof(commandOperations));
    }

    public RuntimeEndpoint RuntimeEndpoint => runtimeAdapter.RuntimeEndpoint;

    public IEndpointAttachmentPropertyOperations PropertyOperations { get; }

    public IEndpointAttachmentCommandOperations CommandOperations { get; }

    internal Task ProbeHealthAsync(CancellationToken cancellationToken = default) =>
        runtimeAdapter.ProbeHealthAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
        {
            disposalTask ??= runtimeAdapter.DisposeAsync().AsTask();
            return new ValueTask(disposalTask);
        }
    }
}
