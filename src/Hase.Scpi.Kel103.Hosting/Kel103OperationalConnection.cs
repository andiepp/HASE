using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103.Runtime;

namespace Hase.Scpi.Kel103.Hosting;

/// <summary>
/// Owns one synchronized, staged KEL-103 runtime endpoint and its serial session.
/// </summary>
public sealed class Kel103OperationalConnection : IAsyncDisposable
{
    private readonly Kel103RuntimeEndpointAdapter runtimeAdapter;
    private readonly object disposalLock = new();
    private Task? disposalTask;

    internal Kel103OperationalConnection(
        Kel103RuntimeEndpointAdapter runtimeAdapter,
        Kel103EndpointAttachmentPropertyOperations propertyOperations)
        : this(
            runtimeAdapter,
            propertyOperations,
            new Kel103EndpointAttachmentCommandOperations(runtimeAdapter))
    {
    }

    internal Kel103OperationalConnection(
        Kel103RuntimeEndpointAdapter runtimeAdapter,
        Kel103EndpointAttachmentPropertyOperations propertyOperations,
        Kel103EndpointAttachmentCommandOperations commandOperations)
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
