using System.Security.Cryptography.X509Certificates;
using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcRecoveringClientSessionFactoryTests
{
    [Fact]
    public async Task CreateAsync_ShouldLoadOptionsAndReturnSession()
    {
        RuntimeHostPrivateNetworkClientOptions options =
            CreateOptions();
        var session =
            new StubSession();
        string? loadedPath =
            null;
        RuntimeHostPrivateNetworkClientOptions? composedOptions =
            null;
        var factory =
            new RuntimeHostGrpcRecoveringClientSessionFactory(
                (path, _) =>
                {
                    loadedPath =
                        path;
                    return Task.FromResult(
                        options);
                },
                value =>
                {
                    composedOptions =
                        value;
                    return session;
                });

        IRuntimeHostClientSession result =
            await factory.CreateAsync(
                @"C:\HASE\client.json");

        Assert.Same(
            session,
            result);
        Assert.Equal(
            @"C:\HASE\client.json",
            loadedPath);
        Assert.Same(
            options,
            composedOptions);
        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            result.Status.State);
    }

    [Fact]
    public async Task CreateAsync_NullPath_ShouldThrow()
    {
        var factory =
            CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(
            "configurationFilePath",
            () =>
                factory.CreateAsync(
                    null!));
    }

    [Fact]
    public async Task CreateAsync_LoaderFailure_ShouldPropagate()
    {
        var expected =
            new InvalidDataException(
                "Invalid external configuration.");
        var factory =
            new RuntimeHostGrpcRecoveringClientSessionFactory(
                (_, _) =>
                    Task.FromException<
                        RuntimeHostPrivateNetworkClientOptions>(
                        expected),
                _ =>
                    new StubSession());

        InvalidDataException actual =
            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    factory.CreateAsync(
                        @"C:\HASE\client.json"));

        Assert.Same(
            expected,
            actual);
    }

    [Fact]
    public async Task CreateAsync_PreCancelled_ShouldNotComposeSession()
    {
        bool composed =
            false;
        using var cancellationSource =
            new CancellationTokenSource();
        cancellationSource.Cancel();
        var factory =
            new RuntimeHostGrpcRecoveringClientSessionFactory(
                (_, _) =>
                    Task.FromResult(
                        CreateOptions()),
                _ =>
                {
                    composed =
                        true;
                    return new StubSession();
                });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                factory.CreateAsync(
                    @"C:\HASE\client.json",
                    cancellationSource.Token));

        Assert.False(
            composed);
    }

    [Fact]
    public async Task CreateAsync_NullSession_ShouldThrow()
    {
        var factory =
            new RuntimeHostGrpcRecoveringClientSessionFactory(
                (_, _) =>
                    Task.FromResult(
                        CreateOptions()),
                _ =>
                    null!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                factory.CreateAsync(
                    @"C:\HASE\client.json"));
    }

    [Fact]
    public void Constructor_NullDependencies_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "loadOptionsAsync",
            () =>
                new RuntimeHostGrpcRecoveringClientSessionFactory(
                    null!,
                    _ =>
                        new StubSession()));
        Assert.Throws<ArgumentNullException>(
            "createSession",
            () =>
                new RuntimeHostGrpcRecoveringClientSessionFactory(
                    (_, _) =>
                        Task.FromResult(
                            CreateOptions()),
                    null!));
    }

    private static RuntimeHostGrpcRecoveringClientSessionFactory
        CreateFactory() =>
        new(
            (_, _) =>
                Task.FromResult(
                    CreateOptions()),
            _ =>
                new StubSession());

    private static RuntimeHostPrivateNetworkClientOptions CreateOptions()
    {
        var clientCertificate =
            new RuntimeHostCertificateStoreReference(
                StoreName.My,
                StoreLocation.CurrentUser,
                "0123456789ABCDEF0123456789ABCDEF01234567");
        var serverCertificate =
            new RuntimeHostCertificateStoreReference(
                StoreName.CertificateAuthority,
                StoreLocation.CurrentUser,
                "89ABCDEF0123456789ABCDEF0123456789ABCDEF");

        return new RuntimeHostPrivateNetworkClientOptions(
            new Uri(
                "https://192.0.2.10:5000"),
            clientCertificate,
            serverCertificate);
    }

    private sealed class StubSession
        : IRuntimeHostClientSession
    {
        public event EventHandler<
            RuntimeHostClientSessionStatusChangedEventArgs>? StatusChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public RuntimeHostClientSessionStatus Status
        {
            get;
        } =
            new(
                RuntimeHostClientSessionState.Disconnected);

        public RemoteObservationState? CurrentState =>
            null;

        public async IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
