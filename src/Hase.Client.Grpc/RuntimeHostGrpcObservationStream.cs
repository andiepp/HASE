using Grpc.Core;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc;

/// <summary>
/// Adapts one generated version 1 gRPC observation call to the
/// transport-independent client observation stream.
/// </summary>
public sealed class RuntimeHostGrpcObservationStream
    : IRemoteObservationStream,
      IAsyncDisposable
{
    private readonly object gate =
        new();
    private readonly Func<
        CancellationToken,
        AsyncServerStreamingCall<GrpcV1.ObserveResponse>> callFactory;
    private readonly RuntimeHostGrpcObservationMapper mapper;
    private AsyncServerStreamingCall<GrpcV1.ObserveResponse>? call;
    private StreamState state;

    /// <summary>
    /// Initializes one single-use observation stream over a generated client.
    /// </summary>
    public RuntimeHostGrpcObservationStream(
        GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient client)
        : this(
            cancellationToken =>
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    cancellationToken:
                        cancellationToken),
            new RuntimeHostGrpcObservationMapper())
    {
        ArgumentNullException.ThrowIfNull(
            client);
    }

    internal RuntimeHostGrpcObservationStream(
        Func<
            CancellationToken,
            AsyncServerStreamingCall<GrpcV1.ObserveResponse>> callFactory,
        RuntimeHostGrpcObservationMapper mapper)
    {
        this.callFactory =
            callFactory
            ?? throw new ArgumentNullException(
                nameof(callFactory));
        this.mapper =
            mapper
            ?? throw new ArgumentNullException(
                nameof(mapper));
    }

    /// <inheritdoc />
    public async ValueTask<RemoteObservationInitialSnapshot>
        ReadInitialSnapshotAsync(
            CancellationToken cancellationToken = default)
    {
        AsyncServerStreamingCall<GrpcV1.ObserveResponse> activeCall;

        lock (gate)
        {
            if (state != StreamState.NotStarted)
            {
                throw new InvalidOperationException(
                    "The gRPC observation initial snapshot can be read only "
                    + "once.");
            }

            state =
                StreamState.ReadingInitial;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            activeCall =
                callFactory(
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The gRPC observation call factory returned null.");

            lock (gate)
            {
                call =
                    activeCall;
            }

            if (!await activeCall.ResponseStream.MoveNext(
                    cancellationToken)
                .ConfigureAwait(
                    false))
            {
                throw new InvalidDataException(
                    "The gRPC observation stream ended before its initial "
                    + "snapshot.");
            }

            GrpcV1.ObserveResponse response =
                activeCall.ResponseStream.Current;

            if (response.ContentCase
                != GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot)
            {
                throw new InvalidDataException(
                    "The first gRPC observation response is not an initial "
                    + "snapshot.");
            }

            RemoteObservationInitialSnapshot initialSnapshot =
                mapper.MapInitialSnapshot(
                    response.InitialSnapshot);

            lock (gate)
            {
                state =
                    StreamState.Initialized;
            }

            return initialSnapshot;
        }
        catch
        {
            DisposeCall();
            throw;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<RemoteRuntimeHostObservation>
        ReadObservationsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
    {
        AsyncServerStreamingCall<GrpcV1.ObserveResponse> activeCall;

        lock (gate)
        {
            if (state != StreamState.Initialized
                || call is null)
            {
                throw new InvalidOperationException(
                    "The gRPC observation initial snapshot must be read "
                    + "before later observations.");
            }

            state =
                StreamState.ReadingObservations;
            activeCall =
                call;
        }

        try
        {
            while (await activeCall.ResponseStream.MoveNext(
                    cancellationToken)
                .ConfigureAwait(
                    false))
            {
                GrpcV1.ObserveResponse response =
                    activeCall.ResponseStream.Current;

                if (response.ContentCase
                    != GrpcV1.ObserveResponse.ContentOneofCase.Observation)
                {
                    throw new InvalidDataException(
                        "A later gRPC observation response is not an "
                        + "observation.");
                }

                yield return mapper.MapObservation(
                    response.Observation);
            }
        }
        finally
        {
            DisposeCall();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        DisposeCall();

        return ValueTask.CompletedTask;
    }

    private void DisposeCall()
    {
        AsyncServerStreamingCall<GrpcV1.ObserveResponse>? callToDispose;

        lock (gate)
        {
            if (state == StreamState.Disposed)
            {
                return;
            }

            state =
                StreamState.Disposed;
            callToDispose =
                call;
            call =
                null;
        }

        callToDispose?.Dispose();
    }

    private enum StreamState
    {
        NotStarted,
        ReadingInitial,
        Initialized,
        ReadingObservations,
        Disposed
    }
}
