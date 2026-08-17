using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Media;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class MutualTlsLoopbackGrpcObservationCompositionTests
{
    [Fact]
    public void Create_MissingObservationService_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        Assert.Throws<ArgumentNullException>(
            "observationService",
            () =>
                MutualTlsLoopbackGrpcHostFactory.Create(
                    new LoopbackGrpcBinding(
                        IPAddress.Loopback,
                        0),
                    RuntimeHostMutualTlsOptions.EnabledWith(
                        certificate),
                    new TestSnapshotProvider(),
                    propertyService: null,
                    commandService: null,
                    observationService: null!,
                    new TestCertificateAuthenticationService()));
    }

    [Fact]
    public async Task Create_WithObservationService_ShouldRegisterObservationComposition()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();
        var observationService =
            new TestObservationService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService,
                new TestCertificateAuthenticationService());

        Assert.Same(
            observationService,
            application.Services.GetRequiredService<
                Northbound.IRuntimeHostObservationService>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IObservationInitialSnapshotMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostObservationMapper>());
        Assert.Null(
            application.Services.GetService<
                IRuntimeHostClientPrincipalProvider>());
        Assert.Null(
            application.Services.GetService<
                IRuntimeHostRemoteAuthorizationGate>());
        Assert.Null(
            application.Services.GetService<RuntimeHostMediaSessionOwner>());
    }

    [Fact]
    public async Task Create_WithDiagnosticProjection_ShouldRegisterDiagnosticComposition()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();
        await using var projection =
            new Northbound.RuntimeHostDiagnosticProjectionService(
                new Northbound.RuntimeHostId("runtime-host-diagnostics"),
                new BoundedRuntimeDiagnosticCollector(8),
                RuntimeDiagnosticLevel.Operational,
                new Northbound.RuntimeHostDiagnosticProjectionPolicy());
        var authorizationPolicy = new RuntimeHostAuthorizationPolicy([]);

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(IPAddress.Loopback, 0),
                RuntimeHostMutualTlsOptions.EnabledWith(certificate),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService: null,
                projection,
                new TestCertificateAuthenticationService(),
                authorizationPolicy: authorizationPolicy);

        Assert.Same(
            projection,
            application.Services.GetRequiredService<
                Northbound.RuntimeHostDiagnosticProjectionService>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                RuntimeHostProjectedDiagnosticObservationMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostClientPrincipalProvider>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostRemoteOperationPermissionMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostAuthorizationService>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostRemoteAuthorizationGate>());
    }

    [Fact]
    public async Task CreateCore_WithMediaOwnerAndPolicy_ShouldRegisterMediaComposition()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();
        var snapshotProvider = new TestSnapshotProvider();
        var observationService = new TestObservationService();
        var authorizationPolicy = new RuntimeHostAuthorizationPolicy([]);
        var boundary = new TestMediaCaptureBoundary();
        await using var owner = new RuntimeHostMediaSessionOwner(
            new RuntimeHostMediaSourceConfiguration(
                new("camera", "generation"),
                "video-device",
                null,
                RuntimeHostMediaSourceAvailability.Idle),
            boundary);

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(IPAddress.Loopback, 0),
                RuntimeHostMutualTlsOptions.EnabledWith(certificate),
                snapshotProvider,
                propertyService: null,
                commandService: null,
                observationService,
                new TestCertificateAuthenticationService(),
                authorizationPolicy: authorizationPolicy,
                mediaSessionOwner: owner);

        Assert.Same(owner, application.Services.GetRequiredService<
            RuntimeHostMediaSessionOwner>());
        Assert.NotNull(application.Services.GetRequiredService<
            RuntimeHostMediaAuthorizationGate>());
        Assert.NotNull(application.Services.GetRequiredService<
            RuntimeHostMediaGrpcMapper>());
        Assert.Equal("runtime-host-c034-composition",
            application.Services.GetRequiredService<string>());
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
                    "runtime-host-c034-composition"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TestObservationService
        : Northbound.IRuntimeHostObservationService
    {
        public Task<Northbound.RuntimeHostObservationSubscription>
            OpenSubscriptionAsync(
                Northbound.RuntimeHostObservationSubscriptionOptions options,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestMediaCaptureBoundary
        : IRuntimeHostMediaCaptureBoundary
    {
        public ValueTask OpenAsync(
            RuntimeHostMediaSourceConfiguration source,
            bool includeAudio,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask SubmitNegotiationAsync(
            RuntimeHostMediaNegotiationMessage message,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
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
}
