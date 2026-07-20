using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointAttachmentSessionTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldExposeValues()
    {
        // Arrange
        EndpointAttachmentRequest request =
            CreateRequest();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        // Act
        var session =
            new EndpointAttachmentSession(
                request,
                runtimeEndpoint,
                Array.Empty<IAsyncDisposable>());

        // Assert
        Assert.Same(
            request,
            session.Request);

        Assert.Same(
            runtimeEndpoint,
            session.RuntimeEndpoint);
    }

    [Fact]
    public void Constructor_NullRequest_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new EndpointAttachmentSession(
                null!,
                CreateRuntimeEndpoint(),
                Array.Empty<IAsyncDisposable>());
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullRuntimeEndpoint_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new EndpointAttachmentSession(
                CreateRequest(),
                null!,
                Array.Empty<IAsyncDisposable>());
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullOwnedResources_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullOwnedResource_ShouldThrow()
    {
        // Arrange
        IAsyncDisposable[] resources =
        [
            null!
        ];

        // Act
        void Act()
        {
            _ = new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                resources);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public async Task ShutdownAsync_ShouldDisposeResourcesInSuppliedOrder()
    {
        // Arrange
        var disposalOrder =
            new List<string>();

        var first =
            new RecordingAsyncDisposable(
                "first",
                disposalOrder);

        var second =
            new RecordingAsyncDisposable(
                "second",
                disposalOrder);

        var third =
            new RecordingAsyncDisposable(
                "third",
                disposalOrder);

        var session =
            new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                [
                    first,
                    second,
                    third
                ]);

        // Act
        await session.ShutdownAsync();

        // Assert
        Assert.Equal(
            new[]
            {
                "first",
                "second",
                "third"
            },
            disposalOrder);
    }

    [Fact]
    public async Task ShutdownAsync_RepeatedCall_ShouldDisposeEachResourceOnce()
    {
        // Arrange
        var resource =
            new RecordingAsyncDisposable(
                "resource",
                []);

        var session =
            new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                [
                    resource
                ]);

        // Act
        await session.ShutdownAsync();
        await session.ShutdownAsync();

        // Assert
        Assert.Equal(
            1,
            resource.DisposeCallCount);
    }

    [Fact]
    public async Task ShutdownAsync_CallerCancellation_ShouldNotAbandonCleanup()
    {
        // Arrange
        var resource =
            new BlockingAsyncDisposable();

        var session =
            new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                [
                    resource
                ]);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task shutdownTask =
            session.ShutdownAsync(
                cancellationTokenSource.Token);

        await resource.DisposalStarted;

        // Act
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => shutdownTask);

        resource.AllowDisposal();

        await session.ShutdownAsync();

        // Assert
        Assert.Equal(
            1,
            resource.DisposeCallCount);
    }

    [Fact]
    public async Task ShutdownAsync_ResourceFailure_ShouldContinueCleanup()
    {
        // Arrange
        var disposalOrder =
            new List<string>();

        var expectedException =
            new InvalidOperationException(
                "First resource failed.");

        var first =
            new RecordingAsyncDisposable(
                "first",
                disposalOrder,
                expectedException);

        var second =
            new RecordingAsyncDisposable(
                "second",
                disposalOrder);

        var session =
            new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                [
                    first,
                    second
                ]);

        // Act
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.ShutdownAsync());

        // Assert
        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            new[]
            {
                "first",
                "second"
            },
            disposalOrder);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedCall_ShouldBeSafe()
    {
        // Arrange
        var resource =
            new RecordingAsyncDisposable(
                "resource",
                []);

        var session =
            new EndpointAttachmentSession(
                CreateRequest(),
                CreateRuntimeEndpoint(),
                [
                    resource
                ]);

        // Act
        await session.DisposeAsync();
        await session.DisposeAsync();

        // Assert
        Assert.Equal(
            1,
            resource.DisposeCallCount);
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
                    "attachment-session-endpoint")));
    }

    private sealed class StubEndpointConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId =>
            null;
    }

    private sealed class RecordingAsyncDisposable
        : IAsyncDisposable
    {
        private readonly string _name;

        private readonly ICollection<string>
            _disposalOrder;

        private readonly Exception? _exception;

        public RecordingAsyncDisposable(
            string name,
            ICollection<string> disposalOrder,
            Exception? exception = null)
        {
            _name =
                name;

            _disposalOrder =
                disposalOrder;

            _exception =
                exception;
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            _disposalOrder.Add(
                _name);

            if (_exception is not null)
            {
                return ValueTask.FromException(
                    _exception);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingAsyncDisposable
        : IAsyncDisposable
    {
        private readonly TaskCompletionSource
            _disposalStarted =
                new(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

        private readonly TaskCompletionSource
            _disposalAllowed =
                new(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

        public Task DisposalStarted =>
            _disposalStarted.Task;

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public void AllowDisposal()
        {
            _disposalAllowed.TrySetResult();
        }

        public async ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            _disposalStarted.TrySetResult();

            await _disposalAllowed.Task;
        }
    }
}