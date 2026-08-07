using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class MutualTlsPrivateNetworkGrpcHostFactoryTests
{
    private static readonly IPAddress ListenerAddress =
        IPAddress.Parse(
            "192.0.2.10");

    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            12,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Create_MissingBinding_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateServerCertificate();

        Assert.Throws<ArgumentNullException>(
            "binding",
            () =>
                MutualTlsPrivateNetworkGrpcHostFactory.Create(
                    null!,
                    RuntimeHostMutualTlsOptions.EnabledWith(
                        certificate),
                    new TestSnapshotProvider(),
                    new TestAuthenticationService(),
                    new FixedTimeProvider()));
    }

    [Fact]
    public void Create_DisabledMutualTls_ShouldThrow()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    MutualTlsPrivateNetworkGrpcHostFactory.Create(
                        CreateBinding(),
                        RuntimeHostMutualTlsOptions.Disabled(),
                        new TestSnapshotProvider(),
                        new TestAuthenticationService(),
                        new FixedTimeProvider()));

        Assert.Contains(
            "server certificate",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_InvalidServerCertificate_ShouldThrowBeforeHostCreation()
    {
        using X509Certificate2 certificate =
            CreateServerCertificate(
                IPAddress.Parse(
                    "192.0.2.11"));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    MutualTlsPrivateNetworkGrpcHostFactory.Create(
                        CreateBinding(),
                        RuntimeHostMutualTlsOptions.EnabledWith(
                            certificate),
                        new TestSnapshotProvider(),
                        new TestAuthenticationService(),
                        new FixedTimeProvider()));

        Assert.Contains(
            "listener address",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ValidConfiguration_ShouldCreateUnstartedHost()
    {
        using X509Certificate2 certificate =
            CreateServerCertificate();

        await using WebApplication application =
            MutualTlsPrivateNetworkGrpcHostFactory.Create(
                CreateBinding(),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate),
                new TestSnapshotProvider(),
                new TestAuthenticationService(),
                new FixedTimeProvider());

        Assert.NotNull(
            application);
    }

    [Fact]
    public async Task Create_DiagnosticProjection_ShouldRegisterOptionalComposition()
    {
        using X509Certificate2 certificate = CreateServerCertificate();
        await using var projection =
            new Northbound.RuntimeHostDiagnosticProjectionService(
                new Northbound.RuntimeHostId("runtime-host-diagnostics"),
                new BoundedRuntimeDiagnosticCollector(8),
                RuntimeDiagnosticLevel.Operational,
                new Northbound.RuntimeHostDiagnosticProjectionPolicy());
        var authorizationPolicy = new RuntimeHostAuthorizationPolicy([]);

        await using WebApplication application =
            MutualTlsPrivateNetworkGrpcHostFactory.Create(
                CreateBinding(),
                RuntimeHostMutualTlsOptions.EnabledWith(certificate),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService: null,
                projection,
                new TestAuthenticationService(),
                new FixedTimeProvider(),
                authorizationPolicy);

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
                IRuntimeHostRemoteAuthorizationGate>());
    }

    private static PrivateNetworkGrpcBinding CreateBinding()
    {
        return new PrivateNetworkGrpcBinding(
            ListenerAddress,
            5000);
    }

    private static X509Certificate2 CreateServerCertificate(
        IPAddress? certificateAddress = null)
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=HASE generated private-network host test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new(
                        "1.3.6.1.5.5.7.3.1")
                },
                true));
        var subjectAlternativeName =
            new SubjectAlternativeNameBuilder();
        subjectAlternativeName.AddIpAddress(
            certificateAddress
            ?? ListenerAddress);
        request.CertificateExtensions.Add(
            subjectAlternativeName.Build());

        return request.CreateSelfSigned(
            ValidationTimeUtc.AddDays(
                -1),
            ValidationTimeUtc.AddDays(
                1));
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return ValidationTimeUtc;
        }
    }

    private sealed class TestAuthenticationService
        : IRuntimeHostCertificateAuthenticationService
    {
        public RuntimeHostCertificateAuthenticationResult Authenticate(
            X509Certificate2? certificate,
            DateTimeOffset authenticatedAtUtc)
        {
            return RuntimeHostCertificateAuthenticationResult
                .CertificateInvalid(
                    RuntimeHostClientCertificateValidationFailureReason
                        .CertificateMissing);
        }
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-private-network-test"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }
}
