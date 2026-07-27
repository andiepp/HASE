using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hase.Client;
using Hase.Client.Grpc;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcObservationStreamTests
{
    [Fact]
    public async Task ReadInitialSnapshotAsync_InitialResponse_ShouldMapSnapshot()
    {
        var call =
            new StubStreamingCall(
                [CreateInitialResponse()]);
        var stream =
            CreateStream(
                call);

        RemoteObservationInitialSnapshot result =
            await stream.ReadInitialSnapshotAsync();

        Assert.Equal(
            "runtime-01",
            result.Snapshot.RuntimeHostId.Value);
        Assert.Equal(
            1UL,
            result.SnapshotSequence.Value);
    }

    [Fact]
    public async Task ReadInitialSnapshotAsync_EmptyStream_ShouldThrowAndDispose()
    {
        var call =
            new StubStreamingCall(
                []);
        var stream =
            CreateStream(
                call);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await stream.ReadInitialSnapshotAsync());

        Assert.True(
            call.IsDisposed);
    }

    [Fact]
    public async Task ReadInitialSnapshotAsync_ObservationFirst_ShouldThrow()
    {
        var call =
            new StubStreamingCall(
                [CreateObservationResponse()]);
        var stream =
            CreateStream(
                call);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await stream.ReadInitialSnapshotAsync());
    }

    [Fact]
    public async Task ReadInitialSnapshotAsync_SecondRead_ShouldThrow()
    {
        var call =
            new StubStreamingCall(
                [CreateInitialResponse()]);
        var stream =
            CreateStream(
                call);
        await stream.ReadInitialSnapshotAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await stream.ReadInitialSnapshotAsync());
    }

    [Fact]
    public async Task ReadObservationsAsync_AfterInitial_ShouldMapObservations()
    {
        var call =
            new StubStreamingCall(
                [
                    CreateInitialResponse(),
                    CreateObservationResponse()
                ]);
        var stream =
            CreateStream(
                call);
        await stream.ReadInitialSnapshotAsync();

        IReadOnlyList<RemoteRuntimeHostObservation> observations =
            await ReadAllAsync(
                stream.ReadObservationsAsync());

        RemoteRuntimeHostObservation observation =
            Assert.Single(
                observations);
        Assert.Equal(
            RemoteObservationKind.AttachmentEnded,
            observation.Kind);
        Assert.True(
            call.IsDisposed);
    }

    [Fact]
    public async Task ReadObservationsAsync_BeforeInitial_ShouldThrow()
    {
        var call =
            new StubStreamingCall(
                []);
        var stream =
            CreateStream(
                call);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ReadAllAsync(
                stream.ReadObservationsAsync()));
    }

    [Fact]
    public async Task ReadObservationsAsync_SecondInitialSnapshot_ShouldThrow()
    {
        var call =
            new StubStreamingCall(
                [
                    CreateInitialResponse(),
                    CreateInitialResponse()
                ]);
        var stream =
            CreateStream(
                call);
        await stream.ReadInitialSnapshotAsync();

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await ReadAllAsync(
                stream.ReadObservationsAsync()));

        Assert.True(
            call.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_ActiveCall_ShouldDisposeIdempotently()
    {
        var call =
            new StubStreamingCall(
                [CreateInitialResponse()]);
        var stream =
            CreateStream(
                call);
        await stream.ReadInitialSnapshotAsync();

        await stream.DisposeAsync();
        await stream.DisposeAsync();

        Assert.True(
            call.IsDisposed);
        Assert.Equal(
            1,
            call.DisposeCount);
    }

    [Fact]
    public async Task ReadInitialSnapshotAsync_ShouldForwardCancellationToken()
    {
        using var cancellation =
            new CancellationTokenSource();
        var call =
            new StubStreamingCall(
                [CreateInitialResponse()]);
        CancellationToken factoryToken =
            default;
        var stream =
            new RuntimeHostGrpcObservationStream(
                token =>
                {
                    factoryToken =
                        token;

                    return call.Call;
                },
                new RuntimeHostGrpcObservationMapper());

        await stream.ReadInitialSnapshotAsync(
            cancellation.Token);

        Assert.Equal(
            cancellation.Token,
            factoryToken);
        Assert.Equal(
            cancellation.Token,
            call.LastMoveNextToken);
    }

    private static RuntimeHostGrpcObservationStream CreateStream(
        StubStreamingCall call)
    {
        return new RuntimeHostGrpcObservationStream(
            _ => call.Call,
            new RuntimeHostGrpcObservationMapper());
    }

    private static async Task<IReadOnlyList<RemoteRuntimeHostObservation>>
        ReadAllAsync(
            IAsyncEnumerable<RemoteRuntimeHostObservation> source)
    {
        var observations =
            new List<RemoteRuntimeHostObservation>();

        await foreach (RemoteRuntimeHostObservation observation
            in source)
        {
            observations.Add(
                observation);
        }

        return observations;
    }

    private static GrpcV1.ObserveResponse CreateInitialResponse()
    {
        return new GrpcV1.ObserveResponse
        {
            InitialSnapshot =
                new GrpcV1.ObservationInitialSnapshot
                {
                    SnapshotSequence =
                        1,
                    Snapshot =
                        new GrpcV1.GetSnapshotResponse
                        {
                            RuntimeHostId =
                                "runtime-01",
                            ApiVersion =
                                new GrpcV1.RuntimeHostApiVersion
                                {
                                    Major =
                                        1,
                                    Minor =
                                        0
                                }
                        }
                }
        };
    }

    private static GrpcV1.ObserveResponse CreateObservationResponse()
    {
        return new GrpcV1.ObserveResponse
        {
            Observation =
                new GrpcV1.RuntimeHostObservation
                {
                    Sequence =
                        2,
                    EndpointId =
                        "endpoint-01",
                    AttachmentGeneration =
                        "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8",
                    Kind =
                        GrpcV1.RuntimeHostObservationKind.AttachmentEnded,
                    AttachmentEnded =
                        new GrpcV1.AttachmentEndedObservation
                        {
                            EndedAtUtc =
                                Timestamp.FromDateTimeOffset(
                                    DateTimeOffset.UnixEpoch)
                        }
                }
        };
    }

    private sealed class StubStreamingCall
    {
        private readonly StubAsyncStreamReader reader;

        public StubStreamingCall(
            IReadOnlyList<GrpcV1.ObserveResponse> responses)
        {
            reader =
                new StubAsyncStreamReader(
                    responses);
            Call =
                new AsyncServerStreamingCall<GrpcV1.ObserveResponse>(
                    reader,
                    Task.FromResult(
                        new Metadata()),
                    () => new Status(
                        StatusCode.OK,
                        string.Empty),
                    () => new Metadata(),
                    () =>
                    {
                        DisposeCount++;
                        IsDisposed =
                            true;
                    });
        }

        public AsyncServerStreamingCall<GrpcV1.ObserveResponse> Call
        {
            get;
        }

        public bool IsDisposed
        {
            get;
            private set;
        }

        public int DisposeCount
        {
            get;
            private set;
        }

        public CancellationToken LastMoveNextToken =>
            reader.LastMoveNextToken;
    }

    private sealed class StubAsyncStreamReader
        : IAsyncStreamReader<GrpcV1.ObserveResponse>
    {
        private readonly Queue<GrpcV1.ObserveResponse> responses;

        public StubAsyncStreamReader(
            IEnumerable<GrpcV1.ObserveResponse> responses)
        {
            this.responses =
                new Queue<GrpcV1.ObserveResponse>(
                    responses);
        }

        public GrpcV1.ObserveResponse Current
        {
            get;
            private set;
        } =
            null!;

        public CancellationToken LastMoveNextToken
        {
            get;
            private set;
        }

        public Task<bool> MoveNext(
            CancellationToken cancellationToken)
        {
            LastMoveNextToken =
                cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            if (responses.Count == 0)
            {
                return Task.FromResult(
                    false);
            }

            Current =
                responses.Dequeue();

            return Task.FromResult(
                true);
        }
    }
}
