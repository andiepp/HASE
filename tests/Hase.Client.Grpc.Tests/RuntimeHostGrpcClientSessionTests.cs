using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Hase.Client;
using Hase.Client.Grpc;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcClientSessionTests
{
    private static readonly Guid Generation =
        Guid.Parse(
            "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8");

    [Fact]
    public async Task ConnectAsync_InitialSnapshot_ShouldPublishConnectedState()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        await using var session =
            CreateSession(
                resources);

        await session.ConnectAsync();

        Assert.Equal(
            RuntimeHostClientSessionState.Connected,
            session.Status.State);
        Assert.Equal(
            "runtime-01",
            session.Status.RuntimeHostId!.Value);
        Assert.Equal(
            RuntimeHostClientApiVersion.Current,
            session.Status.ApiVersion);
        Assert.Equal(
            0UL,
            session.CurrentState!.LastSequence!.Value);
    }

    [Fact]
    public async Task ReadStateChangesAsync_ShouldPublishInitialAndLaterStates()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        await using var session =
            CreateSession(
                resources);
        await using IAsyncEnumerator<RemoteObservationState> states =
            session.ReadStateChangesAsync()
                .GetAsyncEnumerator();

        await session.ConnectAsync();

        Assert.True(
            await states.MoveNextAsync());
        Assert.Empty(
            states.Current.Snapshot!.Attachments);

        resources.Publish(
            CreatePublishedObservation());

        Assert.True(
            await states.MoveNextAsync());
        Assert.Single(
            states.Current.Snapshot!.Attachments);
        Assert.Equal(
            1UL,
            session.CurrentState!.LastSequence!.Value);
    }

    [Fact]
    public async Task ConnectAsync_ShouldForwardConnectionCancellationToken()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        CancellationToken receivedToken =
            default;
        using var cancellation =
            new CancellationTokenSource();
        await using var session =
            new RuntimeHostGrpcClientSession(
                token =>
                {
                    receivedToken =
                        token;

                    return ValueTask.FromResult<
                        IRuntimeHostGrpcSessionResources>(
                        resources);
                });

        await session.ConnectAsync(
            cancellation.Token);

        Assert.True(
            receivedToken.CanBeCanceled);
        Assert.Equal(
            receivedToken,
            resources.InitialCancellationToken);
    }

    [Fact]
    public async Task ConnectAsync_SecondCall_ShouldThrow()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        await using var session =
            CreateSession(
                resources);
        await session.ConnectAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ConnectAsync());
    }

    [Fact]
    public async Task ConnectAsync_ResourceFactoryFailure_ShouldFaultSession()
    {
        var expected =
            new IOException(
                "Resource creation failed.");
        await using var session =
            new RuntimeHostGrpcClientSession(
                _ => ValueTask.FromException<
                    IRuntimeHostGrpcSessionResources>(
                    expected));

        IOException actual =
            await Assert.ThrowsAsync<IOException>(
                () => session.ConnectAsync());

        Assert.Same(
            expected,
            actual);
        Assert.Equal(
            RuntimeHostClientSessionState.Faulted,
            session.Status.State);
    }

    [Fact]
    public async Task ObservationFailure_ShouldFaultCompletionAndStateStream()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        await using var session =
            CreateSession(
                resources);
        await using IAsyncEnumerator<RemoteObservationState> states =
            session.ReadStateChangesAsync()
                .GetAsyncEnumerator();
        await session.ConnectAsync();
        Assert.True(
            await states.MoveNextAsync());
        var expected =
            new IOException(
                "Observation failed.");

        resources.Fail(
            expected);

        IOException completionFailure =
            await Assert.ThrowsAsync<IOException>(
                async () => await session.Completion);
        IOException streamFailure =
            await Assert.ThrowsAsync<IOException>(
                async () => await states.MoveNextAsync().AsTask());

        Assert.Same(
            expected,
            completionFailure);
        Assert.Same(
            expected,
            streamFailure);
        Assert.Equal(
            RuntimeHostClientSessionState.Faulted,
            session.Status.State);
        Assert.Equal(
            1,
            resources.DisposeCount);
    }

    [Fact]
    public async Task RemoteCompletion_ShouldFaultSessionWithoutRetry()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        await using var session =
            CreateSession(
                resources);
        await session.ConnectAsync();

        resources.Complete();
        await session.Completion;

        Assert.Equal(
            RuntimeHostClientSessionState.Faulted,
            session.Status.State);
        Assert.Equal(
            1,
            resources.DisposeCount);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldCancelDisposeAndClearState()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        await using var session =
            CreateSession(
                resources);
        await session.ConnectAsync();

        await session.DisconnectAsync();

        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            session.Status.State);
        Assert.Null(
            session.CurrentState);
        Assert.True(
            resources.ObservationCancellationToken.IsCancellationRequested);
        Assert.Equal(
            1,
            resources.DisposeCount);
    }

    [Fact]
    public async Task DisconnectAsync_RepeatedCall_ShouldBeIdempotent()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        await using var session =
            CreateSession(
                resources);
        await session.ConnectAsync();

        await session.DisconnectAsync();
        await session.DisconnectAsync();

        Assert.Equal(
            1,
            resources.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedCall_ShouldBeIdempotent()
    {
        var resources =
            new StubSessionResources(
                CreateInitialSnapshot());
        var session =
            CreateSession(
                resources);
        await session.ConnectAsync();

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            session.Status.State);
        Assert.Equal(
            1,
            resources.DisposeCount);
    }

    private static RuntimeHostGrpcClientSession CreateSession(
        StubSessionResources resources)
    {
        return new RuntimeHostGrpcClientSession(
            _ => ValueTask.FromResult<
                IRuntimeHostGrpcSessionResources>(
                resources));
    }

    private static RemoteObservationInitialSnapshot CreateInitialSnapshot()
    {
        return new RemoteObservationInitialSnapshot(
            new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "runtime-01"),
                RuntimeHostClientApiVersion.Current,
                []),
            new RemoteObservationSequence(
                0));
    }

    private static RemoteRuntimeHostObservation CreatePublishedObservation()
    {
        var endpoint =
            new RemoteEndpointAttachmentSnapshot(
                new RemoteEndpointAttachmentGeneration(
                    Generation),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-01")),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready));

        return new RemoteRuntimeHostObservation(
            new RemoteObservationSequence(
                1),
            endpoint.Key,
            new RemoteAttachmentPublishedObservationPayload(
                endpoint));
    }

    private sealed class StubSessionResources
        : IRuntimeHostGrpcSessionResources
    {
        private readonly StubObservationStream observationStream;

        public StubSessionResources(
            RemoteObservationInitialSnapshot initialSnapshot)
        {
            observationStream =
                new StubObservationStream(
                    initialSnapshot);
        }

        public IRemoteObservationStream ObservationStream =>
            observationStream;

        public IRuntimeHostGrpcPropertyClient PropertyClient
        {
            get;
        } =
            new StubPropertyClient();

        public IRuntimeHostGrpcCommandClient CommandClient
        {
            get;
        } =
            new StubCommandClient();

        public CancellationToken InitialCancellationToken =>
            observationStream.InitialCancellationToken;

        public CancellationToken ObservationCancellationToken =>
            observationStream.ObservationCancellationToken;

        public int DisposeCount
        {
            get;
            private set;
        }

        public void Publish(
            RemoteRuntimeHostObservation observation)
        {
            observationStream.Publish(
                observation);
        }

        public void Complete()
        {
            observationStream.Complete();
        }

        public void Fail(
            Exception exception)
        {
            observationStream.Fail(
                exception);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubPropertyClient
        : IRuntimeHostGrpcPropertyClient
    {
        public Task<
            Hase.Runtime.Remote.Grpc.V1.PropertyOperationResult>
            ReadPropertyAsync(
                Hase.Runtime.Remote.Grpc.V1
                    .ReadAuthoritativePropertyRequest request,
                CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<
            Hase.Runtime.Remote.Grpc.V1.PropertyOperationResult>
            WritePropertyAsync(
                Hase.Runtime.Remote.Grpc.V1.WritePropertyRequest request,
                CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubCommandClient
        : IRuntimeHostGrpcCommandClient
    {
        public Task<
            Hase.Runtime.Remote.Grpc.V1.CommandOperationResult>
            ExecuteAsync(
                Hase.Runtime.Remote.Grpc.V1.ExecuteCommandRequest request,
                CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubObservationStream
        : IRemoteObservationStream
    {
        private readonly RemoteObservationInitialSnapshot initialSnapshot;
        private readonly Channel<RemoteRuntimeHostObservation> observations =
            Channel.CreateUnbounded<RemoteRuntimeHostObservation>();
        private Exception? failure;

        public StubObservationStream(
            RemoteObservationInitialSnapshot initialSnapshot)
        {
            this.initialSnapshot =
                initialSnapshot;
        }

        public CancellationToken InitialCancellationToken
        {
            get;
            private set;
        }

        public CancellationToken ObservationCancellationToken
        {
            get;
            private set;
        }

        public ValueTask<RemoteObservationInitialSnapshot>
            ReadInitialSnapshotAsync(
                CancellationToken cancellationToken = default)
        {
            InitialCancellationToken =
                cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                initialSnapshot);
        }

        public async IAsyncEnumerable<RemoteRuntimeHostObservation>
            ReadObservationsAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            ObservationCancellationToken =
                cancellationToken;

            await foreach (RemoteRuntimeHostObservation observation
                in observations.Reader.ReadAllAsync(
                        cancellationToken)
                    .ConfigureAwait(
                        false))
            {
                yield return observation;
            }

            if (failure is not null)
            {
                throw failure;
            }
        }

        public void Publish(
            RemoteRuntimeHostObservation observation)
        {
            observations.Writer.TryWrite(
                observation);
        }

        public void Complete()
        {
            observations.Writer.TryComplete();
        }

        public void Fail(
            Exception exception)
        {
            failure =
                exception;
            observations.Writer.TryComplete();
        }
    }
}
