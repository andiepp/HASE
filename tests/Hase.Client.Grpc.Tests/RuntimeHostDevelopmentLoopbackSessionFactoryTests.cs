using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Hase.Client.Diagnostics;
using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostDevelopmentLoopbackSessionFactoryTests
{
    [Fact]
    public async Task CreateAsync_DevelopmentDocument_ShouldComposeDevelopmentSessionAndLabelDiagnostics()
    {
        BoundedClientDiagnosticCollector collector = new(20);
        var developmentOptions =
            new RuntimeHostDevelopmentLoopbackClientOptions(
                new Uri("http://127.0.0.1:52110"));
        var session = new StubSession();
        RuntimeHostDevelopmentLoopbackClientOptions? composedOptions = null;
        bool privateNetworkLoaderCalled = false;
        var factory = new RuntimeHostGrpcRecoveringClientSessionFactory(
            (_, _) =>
            {
                privateNetworkLoaderCalled = true;
                return Task.FromResult(CreatePrivateNetworkOptions());
            },
            _ => new StubSession(),
            (_, _) => Task.FromResult(true),
            (_, _) => Task.FromResult(developmentOptions),
            value =>
            {
                composedOptions = value;
                return session;
            },
            new ClientDiagnosticPublisher(collector));

        IRuntimeHostClientSession result = await factory.CreateAsync(
            @"C:\HASE\development-client.json");

        Assert.IsType<DiagnosticRuntimeHostClientSession>(result);
        Assert.Same(developmentOptions, composedOptions);
        Assert.False(privateNetworkLoaderCalled);

        ClientDiagnosticRecord label = Assert.Single(
            collector.GetSnapshot().Records,
            record =>
                record.EventName == "DevelopmentLoopbackConfigurationActive");
        Assert.Equal(ClientDiagnosticSeverity.Warning, label.Severity);
        Assert.Equal("DevelopmentLoopback", label.Metadata["Profile"]);
        Assert.Contains("no TLS", label.Metadata["Security"]);
        await result.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_NonDevelopmentDocument_ShouldUsePrivateNetworkPathWithoutLabel()
    {
        BoundedClientDiagnosticCollector collector = new(20);
        var session = new StubSession();
        var factory = new RuntimeHostGrpcRecoveringClientSessionFactory(
            (_, _) => Task.FromResult(CreatePrivateNetworkOptions()),
            _ => session,
            (_, _) => Task.FromResult(false),
            (_, _) => throw new InvalidOperationException(
                "The development loader must not be called."),
            _ => throw new InvalidOperationException(
                "The development session factory must not be called."),
            new ClientDiagnosticPublisher(collector));

        IRuntimeHostClientSession result = await factory.CreateAsync(
            @"C:\HASE\client.json");

        Assert.IsType<DiagnosticRuntimeHostClientSession>(result);
        Assert.DoesNotContain(
            collector.GetSnapshot().Records,
            record =>
                record.EventName == "DevelopmentLoopbackConfigurationActive");
        await result.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_LegacyInjectedFactory_ShouldNeverProbeOrUseDevelopmentPath()
    {
        var session = new StubSession();
        string? loadedPath = null;
        var factory = new RuntimeHostGrpcRecoveringClientSessionFactory(
            (path, _) =>
            {
                loadedPath = path;
                return Task.FromResult(CreatePrivateNetworkOptions());
            },
            _ => session);

        IRuntimeHostClientSession result = await factory.CreateAsync(
            @"C:\HASE\development-client.json");

        Assert.Same(session, result);
        Assert.Equal(@"C:\HASE\development-client.json", loadedPath);
    }

    [Fact]
    public async Task CreateAsync_DevelopmentProbeFailure_ShouldRecordFailureAndPropagate()
    {
        BoundedClientDiagnosticCollector collector = new(20);
        var expected = new InvalidDataException("Unreadable configuration.");
        var factory = new RuntimeHostGrpcRecoveringClientSessionFactory(
            (_, _) => Task.FromResult(CreatePrivateNetworkOptions()),
            _ => new StubSession(),
            (_, _) => Task.FromException<bool>(expected),
            (_, _) => throw new InvalidOperationException(
                "The development loader must not be called."),
            _ => throw new InvalidOperationException(
                "The development session factory must not be called."),
            new ClientDiagnosticPublisher(collector));

        InvalidDataException actual =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => factory.CreateAsync(@"C:\HASE\client.json"));

        Assert.Same(expected, actual);
        ClientDiagnosticRecord failed = collector.GetSnapshot().Records[^1];
        Assert.Equal("ConfigurationLoadFailed", failed.EventName);
        Assert.Equal(ClientDiagnosticOutcome.Failed, failed.Outcome);
    }

    [Fact]
    public async Task CreateAsync_RealDevelopmentFile_ShouldComposeRecoveringSession()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"hase-60c2-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                """
                {
                  "formatVersion": 1,
                  "profileKind": "development-loopback",
                  "address": "http://127.0.0.1:52110"
                }
                """);

            BoundedClientDiagnosticCollector collector = new(20);
            var factory = new RuntimeHostGrpcRecoveringClientSessionFactory(
                new ClientDiagnosticPublisher(collector));

            IRuntimeHostClientSession result =
                await factory.CreateAsync(filePath);

            Assert.IsType<DiagnosticRuntimeHostClientSession>(result);
            Assert.Equal(
                RuntimeHostClientSessionState.Disconnected,
                result.Status.State);
            Assert.Contains(
                collector.GetSnapshot().Records,
                record =>
                    record.EventName
                    == "DevelopmentLoopbackConfigurationActive");
            await result.DisposeAsync();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static RuntimeHostPrivateNetworkClientOptions
        CreatePrivateNetworkOptions()
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
            new Uri("https://192.0.2.10:5000"),
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
            [EnumeratorCancellation] CancellationToken cancellationToken =
                default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
