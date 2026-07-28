using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Execution;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Defines an endpoint whose complete engineering model and execution
/// boundary live in the runtime host process.
/// </summary>
public sealed class InProcessEndpointConnectionDefinition
    : IEndpointConnectionDefinition
{
    private readonly Func<RuntimeInstrument, IInstrumentExecutor>
        executorFactory;

    public InProcessEndpointConnectionDefinition(
        EndpointDescriptor descriptor,
        Func<RuntimeInstrument, IInstrumentExecutor> executorFactory)
    {
        Descriptor =
            descriptor
            ?? throw new ArgumentNullException(
                nameof(descriptor));

        this.executorFactory =
            executorFactory
            ?? throw new ArgumentNullException(
                nameof(executorFactory));
    }

    public EndpointDescriptor Descriptor
    {
        get;
    }

    public EndpointConnectionOrigin Origin =>
        EndpointConnectionOrigin.Configured;

    public EndpointId ExpectedEndpointId =>
        Descriptor.Id;

    internal IInstrumentExecutor CreateExecutor(
        RuntimeInstrument runtimeInstrument)
    {
        IInstrumentExecutor executor =
            executorFactory(
                runtimeInstrument);

        return executor
            ?? throw new InvalidOperationException(
                "The in-process executor factory returned null.");
    }
}
