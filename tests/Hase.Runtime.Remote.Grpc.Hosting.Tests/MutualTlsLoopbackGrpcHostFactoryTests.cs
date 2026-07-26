using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class MutualTlsLoopbackGrpcHostFactoryTests
{
    [Fact]
    public void Create_MissingBinding_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        Assert.Throws<ArgumentNullException>(
            () => MutualTlsLoopbackGrpcHostFactory.Create(
                null!,
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                new TestCertificateAuthenticationService()));
    }

    [Fact]
    public void Create_MissingMutualTlsOptions_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                null!,
                new TestSnapshotProvider(),
                new TestCertificateAuthenticationService()));
    }

    [Fact]
    public void Create_DisabledMutualTlsOptions_ShouldReject()
    {
        Assert.Throws<InvalidOperationException>(
            () => MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.Disabled(),
                new TestSnapshotProvider(),
                new TestCertificateAuthenticationService()));
    }

    [Fact]
    public void Create_MissingSnapshotProvider_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        Assert.Throws<ArgumentNullException>(
            () => MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                null!,
                new TestCertificateAuthenticationService()));
    }

    [Fact]
    public void Create_MissingCertificateAuthenticationService_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        Assert.Throws<ArgumentNullException>(
            () => MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                null!));
    }

    [Fact]
    public void Create_MissingPropertyService_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        Assert.Throws<ArgumentNullException>(
            () => MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                null!,
                new TestCertificateAuthenticationService()));
    }

    [Fact]
    public void Create_MissingCommandService_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        Assert.Throws<ArgumentNullException>(
            () => MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null!,
                new TestCertificateAuthenticationService()));
    }

    [Fact]
    public async Task Create_ShouldRegisterAuthenticationComposition()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();
        var authenticationService =
            new TestCertificateAuthenticationService();
        var timeProvider =
            new FixedTimeProvider(
                new DateTimeOffset(
                    2026,
                    7,
                    26,
                    8,
                    30,
                    0,
                    TimeSpan.Zero));

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                authenticationService,
                timeProvider);

        Assert.Same(
            authenticationService,
            application.Services.GetRequiredService<
                IRuntimeHostCertificateAuthenticationService>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                RuntimeHostMutualTlsClientCertificateAuthenticator>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                RuntimeHostHttpContextIdentityProjector>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                RuntimeHostMutualTlsRequestAuthenticator>());
        Assert.Same(
            timeProvider,
            application.Services.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task Create_WithPropertyService_ShouldRegisterPropertyComposition()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();
        var propertyService =
            new TestPropertyService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                propertyService,
                new TestCertificateAuthenticationService());

        Assert.Same(
            propertyService,
            application.Services.GetRequiredService<
                Northbound.IRuntimeHostPropertyService>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostPropertyTargetMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostCachedPropertyResultMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostPropertyOperationResultMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRemoteValueMapper>());
    }

    [Fact]
    public async Task Create_WithCommandService_ShouldRegisterCommandComposition()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();
        var commandService =
            new TestCommandService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService:
                    commandService,
                new TestCertificateAuthenticationService());

        Assert.Same(
            commandService,
            application.Services.GetRequiredService<
                Northbound.IRuntimeHostCommandService>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostCommandTargetMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostCommandOperationResultMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRemoteValueMapper>());
    }

    [Fact]
    public async Task Create_WithPropertyAndCommandServices_ShouldShareRemoteValueMapper()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();
        var propertyService =
            new TestPropertyService();
        var commandService =
            new TestCommandService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                propertyService,
                commandService,
                new TestCertificateAuthenticationService());

        Assert.Same(
            propertyService,
            application.Services.GetRequiredService<
                Northbound.IRuntimeHostPropertyService>());
        Assert.Same(
            commandService,
            application.Services.GetRequiredService<
                Northbound.IRuntimeHostCommandService>());
        Assert.Single(
            application.Services.GetServices<IRemoteValueMapper>());
    }

    [Fact]
    public async Task Create_ShouldMapGrpcService()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                new TestCertificateAuthenticationService());

        IEndpointRouteBuilder routeBuilder =
            application;

        Assert.NotEmpty(
            routeBuilder.DataSources);
    }

    private static X509Certificate2 CreateSelfSignedServerCertificate()
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=hase-runtime-host",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                false,
                false,
                0,
                true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new("1.3.6.1.5.5.7.3.1")
                },
                true));

        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(
            nowUtc.AddMinutes(
                -1),
            nowUtc.AddDays(
                1));
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-1"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TestCertificateAuthenticationService
        : IRuntimeHostCertificateAuthenticationService
    {
        public RuntimeHostCertificateAuthenticationResult Authenticate(
            X509Certificate2? certificate,
            DateTimeOffset authenticatedAtUtc)
        {
            return RuntimeHostCertificateAuthenticationResult
                .UnknownCredential();
        }
    }

    private sealed class TestPropertyService
        : Northbound.IRuntimeHostPropertyService
    {
        public Northbound.RuntimeHostCachedPropertyResult GetCached(
            Northbound.RuntimeHostPropertyTarget target)
        {
            throw new NotSupportedException();
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> ReadAsync(
            Northbound.RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> WriteAsync(
            Northbound.RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestCommandService
        : Northbound.IRuntimeHostCommandService
    {
        public Task<Northbound.RuntimeHostCommandOperationResult> ExecuteAsync(
            Northbound.RuntimeHostCommandTarget target,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            this.utcNow =
                utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
