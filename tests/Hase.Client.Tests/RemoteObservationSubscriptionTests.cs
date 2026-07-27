using System.Runtime.CompilerServices;
using Hase.Client;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemoteObservationSubscriptionTests
{
    private static readonly Guid Generation =
        Guid.Parse(
            "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8");

    [Fact]
    public async Task ReadStatesAsync_InitialSnapshot_ShouldYieldInitialState()
    {
        var stream =
            new StubObservationStream(
                CreateInitial(),
                []);

        IReadOnlyList<RemoteObservationState> states =
            await ReadAllAsync(
                new RemoteObservationSubscription().ReadStatesAsync(
                    stream));

        RemoteObservationState state =
            Assert.Single(
                states);
        Assert.True(
            state.IsInitialized);
        Assert.Equal(
            0UL,
            state.LastSequence!.Value);
        Assert.Empty(
            state.Snapshot!.Attachments);
    }

    [Fact]
    public async Task ReadStatesAsync_OrderedObservations_ShouldYieldEveryState()
    {
        RemoteEndpointAttachmentSnapshot endpoint =
            CreateEndpoint();
        var stream =
            new StubObservationStream(
                CreateInitial(),
                [
                    new RemoteRuntimeHostObservation(
                        new RemoteObservationSequence(
                            1),
                        endpoint.Key,
                        new RemoteAttachmentPublishedObservationPayload(
                            endpoint)),
                    new RemoteRuntimeHostObservation(
                        new RemoteObservationSequence(
                            2),
                        endpoint.Key,
                        new RemoteAttachmentEndedObservationPayload(
                            DateTimeOffset.UnixEpoch))
                ]);

        IReadOnlyList<RemoteObservationState> states =
            await ReadAllAsync(
                new RemoteObservationSubscription().ReadStatesAsync(
                    stream));

        Assert.Equal(
            3,
            states.Count);
        Assert.Empty(
            states[0].Snapshot!.Attachments);
        Assert.Single(
            states[1].Snapshot!.Attachments);
        Assert.Empty(
            states[2].Snapshot!.Attachments);
        Assert.Equal(
            2UL,
            states[2].LastSequence!.Value);
    }

    [Fact]
    public async Task ReadStatesAsync_NormalCompletion_ShouldComplete()
    {
        var stream =
            new StubObservationStream(
                CreateInitial(),
                []);

        await using IAsyncEnumerator<RemoteObservationState> enumerator =
            new RemoteObservationSubscription()
                .ReadStatesAsync(
                    stream)
                .GetAsyncEnumerator();

        Assert.True(
            await enumerator.MoveNextAsync());
        Assert.False(
            await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task ReadStatesAsync_NullStream_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            "stream",
            async () => await ReadAllAsync(
                new RemoteObservationSubscription().ReadStatesAsync(
                    null!)));
    }

    [Fact]
    public async Task ReadStatesAsync_InitialSnapshotFailure_ShouldPropagate()
    {
        var expected =
            new IOException(
                "Initial snapshot failed.");
        var stream =
            new ThrowingInitialObservationStream(
                expected);

        IOException actual =
            await Assert.ThrowsAsync<IOException>(
                async () => await ReadAllAsync(
                    new RemoteObservationSubscription().ReadStatesAsync(
                        stream)));

        Assert.Same(
            expected,
            actual);
    }

    [Fact]
    public async Task ReadStatesAsync_ObservationFailure_ShouldPropagate()
    {
        var expected =
            new IOException(
                "Observation stream failed.");
        var stream =
            new ThrowingObservationStream(
                CreateInitial(),
                expected);

        IOException actual =
            await Assert.ThrowsAsync<IOException>(
                async () => await ReadAllAsync(
                    new RemoteObservationSubscription().ReadStatesAsync(
                        stream)));

        Assert.Same(
            expected,
            actual);
    }

    [Fact]
    public async Task ReadStatesAsync_InvalidSequence_ShouldPropagate()
    {
        RemoteEndpointAttachmentSnapshot endpoint =
            CreateEndpoint();
        var stream =
            new StubObservationStream(
                CreateInitial(),
                [
                    new RemoteRuntimeHostObservation(
                        new RemoteObservationSequence(
                            0),
                        endpoint.Key,
                        new RemoteAttachmentPublishedObservationPayload(
                            endpoint))
                ]);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await ReadAllAsync(
                new RemoteObservationSubscription().ReadStatesAsync(
                    stream)));
    }

    [Fact]
    public async Task ReadStatesAsync_CancellationBeforeInitialSnapshot_ShouldPropagate()
    {
        using var cancellation =
            new CancellationTokenSource();
        cancellation.Cancel();
        var stream =
            new CancellationAwareObservationStream(
                CreateInitial());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await ReadAllAsync(
                new RemoteObservationSubscription().ReadStatesAsync(
                    stream,
                    cancellation.Token)));

        Assert.Equal(
            cancellation.Token,
            stream.InitialCancellationToken);
    }

    [Fact]
    public async Task ReadStatesAsync_CancellationDuringObservations_ShouldPropagate()
    {
        using var cancellation =
            new CancellationTokenSource();
        var stream =
            new CancellationAwareObservationStream(
                CreateInitial());
        await using IAsyncEnumerator<RemoteObservationState> enumerator =
            new RemoteObservationSubscription()
                .ReadStatesAsync(
                    stream,
                    cancellation.Token)
                .GetAsyncEnumerator();

        Assert.True(
            await enumerator.MoveNextAsync());

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await enumerator.MoveNextAsync().AsTask());
        Assert.Equal(
            cancellation.Token,
            stream.ObservationCancellationToken);
    }

    [Fact]
    public async Task ReadStatesAsync_ShouldPassTokenToBothTransportStages()
    {
        using var cancellation =
            new CancellationTokenSource();
        var stream =
            new StubObservationStream(
                CreateInitial(),
                []);

        await ReadAllAsync(
            new RemoteObservationSubscription().ReadStatesAsync(
                stream,
                cancellation.Token));

        Assert.Equal(
            cancellation.Token,
            stream.InitialCancellationToken);
        Assert.Equal(
            cancellation.Token,
            stream.ObservationCancellationToken);
    }

    private static async Task<IReadOnlyList<RemoteObservationState>>
        ReadAllAsync(
            IAsyncEnumerable<RemoteObservationState> source)
    {
        var states =
            new List<RemoteObservationState>();

        await foreach (RemoteObservationState state
            in source)
        {
            states.Add(
                state);
        }

        return states;
    }

    private static RemoteObservationInitialSnapshot CreateInitial()
    {
        return new RemoteObservationInitialSnapshot(
            new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    "runtime-01"),
                new RuntimeHostClientApiVersion(
                    1,
                    0),
                []),
            new RemoteObservationSequence(
                0));
    }

    private static RemoteEndpointAttachmentSnapshot CreateEndpoint()
    {
        return new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(
                Generation),
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-01")),
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready));
    }

    private sealed class StubObservationStream
        : IRemoteObservationStream
    {
        private readonly RemoteObservationInitialSnapshot _initialSnapshot;
        private readonly IReadOnlyList<RemoteRuntimeHostObservation>
            _observations;

        public StubObservationStream(
            RemoteObservationInitialSnapshot initialSnapshot,
            IReadOnlyList<RemoteRuntimeHostObservation> observations)
        {
            _initialSnapshot =
                initialSnapshot;
            _observations =
                observations;
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

            return ValueTask.FromResult(
                _initialSnapshot);
        }

        public async IAsyncEnumerable<RemoteRuntimeHostObservation>
            ReadObservationsAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            ObservationCancellationToken =
                cancellationToken;

            foreach (RemoteRuntimeHostObservation observation
                in _observations)
            {
                yield return observation;
            }

            await Task.CompletedTask;
        }
    }

    private sealed class ThrowingInitialObservationStream
        : IRemoteObservationStream
    {
        private readonly Exception _exception;

        public ThrowingInitialObservationStream(
            Exception exception)
        {
            _exception =
                exception;
        }

        public ValueTask<RemoteObservationInitialSnapshot>
            ReadInitialSnapshotAsync(
                CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<
                RemoteObservationInitialSnapshot>(
                _exception);
        }

        public async IAsyncEnumerable<RemoteRuntimeHostObservation>
            ReadObservationsAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class ThrowingObservationStream
        : IRemoteObservationStream
    {
        private readonly RemoteObservationInitialSnapshot _initialSnapshot;
        private readonly Exception _exception;

        public ThrowingObservationStream(
            RemoteObservationInitialSnapshot initialSnapshot,
            Exception exception)
        {
            _initialSnapshot =
                initialSnapshot;
            _exception =
                exception;
        }

        public ValueTask<RemoteObservationInitialSnapshot>
            ReadInitialSnapshotAsync(
                CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                _initialSnapshot);
        }

        public async IAsyncEnumerable<RemoteRuntimeHostObservation>
            ReadObservationsAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw _exception;
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class CancellationAwareObservationStream
        : IRemoteObservationStream
    {
        private readonly RemoteObservationInitialSnapshot _initialSnapshot;

        public CancellationAwareObservationStream(
            RemoteObservationInitialSnapshot initialSnapshot)
        {
            _initialSnapshot =
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
                _initialSnapshot);
        }

        public async IAsyncEnumerable<RemoteRuntimeHostObservation>
            ReadObservationsAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            ObservationCancellationToken =
                cancellationToken;

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            yield break;
        }
    }
}
