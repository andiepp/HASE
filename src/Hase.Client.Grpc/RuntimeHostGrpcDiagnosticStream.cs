using System.Runtime.CompilerServices;
using Grpc.Core;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc;

/// <summary>Owns one single-use version 1 projected diagnostic subscription.</summary>
public sealed class RuntimeHostGrpcDiagnosticStream
    : IRemoteRuntimeDiagnosticStream
{
    private readonly object gate = new();
    private readonly Func<CancellationToken, AsyncServerStreamingCall<
        GrpcV1.ProjectedDiagnosticObservation>> callFactory;
    private readonly RuntimeHostGrpcDiagnosticMapper mapper;
    private AsyncServerStreamingCall<GrpcV1.ProjectedDiagnosticObservation>? call;
    private bool started;
    private bool disposed;

    public RuntimeHostGrpcDiagnosticStream(
        GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiClient client)
        : this(
            cancellationToken => client.ObserveDiagnostics(
                new GrpcV1.ObserveDiagnosticsRequest(),
                cancellationToken: cancellationToken),
            new RuntimeHostGrpcDiagnosticMapper())
    {
        ArgumentNullException.ThrowIfNull(client);
    }

    internal RuntimeHostGrpcDiagnosticStream(
        Func<CancellationToken, AsyncServerStreamingCall<
            GrpcV1.ProjectedDiagnosticObservation>> callFactory,
        RuntimeHostGrpcDiagnosticMapper mapper)
    {
        this.callFactory = callFactory
            ?? throw new ArgumentNullException(nameof(callFactory));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async IAsyncEnumerable<RemoteRuntimeDiagnosticObservation> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AsyncServerStreamingCall<GrpcV1.ProjectedDiagnosticObservation> active;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (started)
            {
                throw new InvalidOperationException(
                    "A projected diagnostic stream can be consumed only once.");
            }
            started = true;
            active = callFactory(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The projected diagnostic call factory returned null.");
            call = active;
        }

        long lastSequence = 0;
        try
        {
            while (await active.ResponseStream.MoveNext(cancellationToken)
                .ConfigureAwait(false))
            {
                RemoteRuntimeDiagnosticObservation observation =
                    mapper.Map(active.ResponseStream.Current);
                if (observation.Sequence != lastSequence + 1)
                {
                    throw new InvalidDataException(
                        "The projected diagnostic stream has a gap. Open a new subscription.");
                }
                lastSequence = observation.Sequence;
                yield return observation;
            }
        }
        finally
        {
            DisposeCall();
        }
    }

    public ValueTask DisposeAsync()
    {
        DisposeCall();
        return ValueTask.CompletedTask;
    }

    private void DisposeCall()
    {
        AsyncServerStreamingCall<GrpcV1.ProjectedDiagnosticObservation>? value;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            value = call;
            call = null;
        }
        value?.Dispose();
    }
}
