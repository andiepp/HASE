using Grpc.Core;
using Hase.Client;
using Hase.Client.Grpc;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcDiagnosticStreamTests
{
    [Fact]
    public async Task ReadAsync_ContiguousRecords_ShouldMapAndDispose()
    {
        var call = new StubCall(
            [
                RuntimeHostGrpcDiagnosticMapperTests.CreateObservation(1),
                RuntimeHostGrpcDiagnosticMapperTests.CreateObservation(2)
            ]);
        await using RuntimeHostGrpcDiagnosticStream stream = Create(call);

        List<RemoteRuntimeDiagnosticObservation> result =
            await ReadAllAsync(stream.ReadAsync());

        Assert.Equal(new long[] { 1, 2 }, result.Select(item => item.Sequence));
        Assert.True(call.IsDisposed);
    }

    [Fact]
    public async Task ReadAsync_Gap_ShouldRejectAndDispose()
    {
        var call = new StubCall(
            [RuntimeHostGrpcDiagnosticMapperTests.CreateObservation(2)]);
        await using RuntimeHostGrpcDiagnosticStream stream = Create(call);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadAllAsync(stream.ReadAsync()));

        Assert.True(call.IsDisposed);
    }

    [Fact]
    public async Task ReadAsync_SecondConsumption_ShouldReject()
    {
        var call = new StubCall([]);
        await using RuntimeHostGrpcDiagnosticStream stream = Create(call);
        await ReadAllAsync(stream.ReadAsync());

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => ReadAllAsync(stream.ReadAsync()));
    }

    [Fact]
    public async Task ReadAsync_ShouldForwardCancellationToken()
    {
        var call = new StubCall([]);
        await using RuntimeHostGrpcDiagnosticStream stream = Create(call);
        using var cancellation = new CancellationTokenSource();

        await ReadAllAsync(stream.ReadAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, call.LastToken);
    }

    private static RuntimeHostGrpcDiagnosticStream Create(StubCall call) =>
        new(_ => call.Call, new RuntimeHostGrpcDiagnosticMapper());

    private static async Task<List<RemoteRuntimeDiagnosticObservation>> ReadAllAsync(
        IAsyncEnumerable<RemoteRuntimeDiagnosticObservation> source)
    {
        var result = new List<RemoteRuntimeDiagnosticObservation>();
        await foreach (RemoteRuntimeDiagnosticObservation item in source)
        {
            result.Add(item);
        }
        return result;
    }

    private sealed class StubCall
    {
        private readonly Reader reader;

        public StubCall(IEnumerable<GrpcV1.ProjectedDiagnosticObservation> values)
        {
            reader = new Reader(values);
            Call = new AsyncServerStreamingCall<GrpcV1.ProjectedDiagnosticObservation>(
                reader,
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.OK, string.Empty),
                () => new Metadata(),
                () => IsDisposed = true);
        }

        public AsyncServerStreamingCall<GrpcV1.ProjectedDiagnosticObservation> Call { get; }
        public bool IsDisposed { get; private set; }
        public CancellationToken LastToken => reader.LastToken;
    }

    private sealed class Reader : IAsyncStreamReader<GrpcV1.ProjectedDiagnosticObservation>
    {
        private readonly Queue<GrpcV1.ProjectedDiagnosticObservation> values;
        public Reader(IEnumerable<GrpcV1.ProjectedDiagnosticObservation> values) =>
            this.values = new(values);
        public GrpcV1.ProjectedDiagnosticObservation Current { get; private set; } = null!;
        public CancellationToken LastToken { get; private set; }
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            LastToken = cancellationToken;
            if (values.Count == 0) return Task.FromResult(false);
            Current = values.Dequeue();
            return Task.FromResult(true);
        }
    }
}
