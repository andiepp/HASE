using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointAttachmentSessionCommandPortTests
{
    [Fact]
    public void Constructor_ExplicitCommandOperations_ExposesSamePort()
    {
        IEndpointAttachmentCommandOperations commandOperations =
            new TestCommandOperations();

        var session =
            new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                new TestPropertyOperations(),
                commandOperations,
                Array.Empty<IAsyncDisposable>());

        Assert.Same(
            commandOperations,
            session.CommandOperations);
    }

    [Fact]
    public void Constructor_NullCommandOperations_Throws()
    {
        void Act()
        {
            _ = new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                new TestPropertyOperations(),
                null!,
                Array.Empty<IAsyncDisposable>());
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task ExistingPropertyConstructor_UsesUnavailableCommandPort()
    {
        var session =
            new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                new TestPropertyOperations(),
                Array.Empty<IAsyncDisposable>());

        EndpointAttachmentCommandOperationResult result =
            await session.CommandOperations.ExecuteAsync(
                new InstrumentId(
                    "instrument-one"),
                new DescriptorPath(
                    "Controller",
                    "ToggleLed"),
                argument: null);

        Assert.Equal(
            EndpointAttachmentCommandOperationStatus.Unavailable,
            result.Status);
    }

    private static EndpointAttachmentRequest CreateRequest()
    {
        return new EndpointAttachmentRequest(
            new StubEndpointConnectionDefinition(),
            EndpointProvidedDescriptorSource.Instance);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            new EndpointDescriptor(
                new EndpointId(
                    "attachment-session-command-endpoint")));
    }

    private sealed class StubEndpointConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId =>
            null;
    }

    private sealed class TestPropertyOperations
        : IEndpointAttachmentPropertyOperations
    {
        public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestCommandOperations
        : IEndpointAttachmentCommandOperations
    {
        public Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
            InstrumentId instrumentId,
            DescriptorPath commandPath,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                EndpointAttachmentCommandOperationResult.Successful());
        }
    }
}