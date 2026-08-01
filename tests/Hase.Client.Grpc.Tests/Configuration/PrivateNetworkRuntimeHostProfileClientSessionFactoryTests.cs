using Hase.Client.Configuration;
using Hase.Client.Grpc.Configuration;

namespace Hase.Client.Grpc.Tests.Configuration;

public sealed class PrivateNetworkRuntimeHostProfileClientSessionFactoryTests
{
    [Fact]
    public async Task CreateAsync_EnabledProfile_ShouldDelegateExactPath()
    {
        string path = Path.GetFullPath("client.json");
        var inner = new FakeFactory();
        var factory = new PrivateNetworkRuntimeHostProfileClientSessionFactory(
            CreateRegistry("first", true, path), inner);

        IRuntimeHostClientSession session = await factory.CreateAsync(new RuntimeHostProfileId("first"));

        Assert.Same(inner.Session, session);
        Assert.Equal(path, inner.Path);
    }

    [Fact]
    public async Task CreateAsync_UnknownProfile_ShouldThrow()
    {
        var factory = new PrivateNetworkRuntimeHostProfileClientSessionFactory(
            CreateRegistry("first", true, Path.GetFullPath("client.json")), new FakeFactory());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => factory.CreateAsync(new RuntimeHostProfileId("missing")));
    }

    [Fact]
    public async Task CreateAsync_DisabledProfile_ShouldThrow()
    {
        var factory = new PrivateNetworkRuntimeHostProfileClientSessionFactory(
            CreateRegistry("first", false, Path.GetFullPath("client.json")), new FakeFactory());
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync(new RuntimeHostProfileId("first")));
    }

    private static PrivateNetworkRuntimeHostProfileRegistry CreateRegistry(string id, bool enabled, string path) =>
        new([new PrivateNetworkRuntimeHostProfile(
            new RuntimeHostProfile(new RuntimeHostProfileId(id), id, new RemoteRuntimeHostId("host-01"), enabled), path)]);

    private sealed class FakeFactory : IRuntimeHostClientSessionFactory
    {
        public FakeSession Session { get; } = new();
        public string? Path { get; private set; }
        public Task<IRuntimeHostClientSession> CreateAsync(string configurationFilePath, CancellationToken cancellationToken = default)
        { Path = configurationFilePath; return Task.FromResult<IRuntimeHostClientSession>(Session); }
    }

    private sealed class FakeSession : IRuntimeHostClientSession
    {
        public event EventHandler<RuntimeHostClientSessionStatusChangedEventArgs>? StatusChanged;
        public RuntimeHostClientSessionStatus Status { get; } = new(RuntimeHostClientSessionState.Disconnected);
        public RemoteObservationState? CurrentState => null;
        public async IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
