using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class MutualTlsLoopbackGrpcObservationIntegrationTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            16,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task Observe_EnrolledClientCertificate_ShouldStreamAndDisposeSubscription()
    {
        using X509Certificate2 certificateAuthority =
            CreateCertificateAuthority();
        using X509Certificate2 serverCertificate =
            CreateServerCertificate(
                certificateAuthority);
        using X509Certificate2 clientCertificate =
            CreateClientCertificate(
                certificateAuthority);

        var identityExtractor =
            new RuntimeHostX509ClientCredentialIdentityExtractor();
        RuntimeHostClientCredentialIdentity credentialIdentity =
            identityExtractor.Extract(
                clientCertificate);
        var enrollmentRegistry =
            new RuntimeHostClientCredentialEnrollmentRegistry(
                new[]
                {
                    new RuntimeHostClientCredentialEnrollment(
                        credentialIdentity,
                        new RuntimeHostClientPrincipalId(
                            "client-01"),
                        "c034-integration-trust-v1")
                });
        var certificateAuthenticationService =
            new RuntimeHostCertificateAuthenticationService(
                new RuntimeHostClientCertificateValidator(),
                new RuntimeHostCertificateTrustValidator(
                    new ExactCertificateTrustEvaluator(
                        clientCertificate)),
                identityExtractor,
                new RuntimeHostClientAuthenticationService(
                    enrollmentRegistry));
        var observationService =
            new TestObservationService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    serverCertificate),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService,
                certificateAuthenticationService,
                new FixedTimeProvider(
                    AuthenticationTimeUtc));

        await application.StartAsync();

        try
        {
            Uri address =
                GetListeningAddress(
                    application);

            using var handler =
                new SocketsHttpHandler();
            handler.SslOptions.EnabledSslProtocols =
                SslProtocols.Tls12
                | SslProtocols.Tls13;
            handler.SslOptions.RemoteCertificateValidationCallback =
                static (_, _, _, _) => true;
            handler.SslOptions.LocalCertificateSelectionCallback =
                (_, _, _, _, _) =>
                    clientCertificate;

            using GrpcChannel channel =
                GrpcChannel.ForAddress(
                    address,
                    new GrpcChannelOptions
                    {
                        HttpHandler =
                            handler
                    });
            var client =
                new GrpcV1.RuntimeHostRemoteApi
                    .RuntimeHostRemoteApiClient(
                        channel);

            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));
            var messages =
                new List<GrpcV1.ObserveResponse>();

            while (await call.ResponseStream.MoveNext())
            {
                messages.Add(
                    call.ResponseStream.Current);
            }

            Assert.Collection(
                messages,
                initial =>
                {
                    Assert.Equal(
                        GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot,
                        initial.ContentCase);
                    Assert.Equal(
                        "runtime-host-c034-secure-observation",
                        initial.InitialSnapshot.Snapshot.RuntimeHostId);
                    Assert.Equal(
                        0UL,
                        initial.InitialSnapshot.SnapshotSequence);
                },
                observation =>
                {
                    Assert.Equal(
                        GrpcV1.ObserveResponse.ContentOneofCase.Observation,
                        observation.ContentCase);
                    Assert.Equal(
                        1UL,
                        observation.Observation.Sequence);
                    Assert.Equal(
                        GrpcV1.RuntimeHostObservationKind.EventOccurred,
                        observation.Observation.Kind);
                    Assert.Equal(
                        "pressed",
                        observation.Observation.EventOccurred.Value.StringValue);
                });
            Assert.Equal(
                1,
                observationService.OpenCount);
            Assert.True(
                observationService.CancellationToken.CanBeCanceled);
            Assert.Equal(
                1,
                observationService.Subscription.DisposeCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task Observe_MissingClientCertificate_ShouldRejectBeforeSubscription()
    {
        using X509Certificate2 certificateAuthority =
            CreateCertificateAuthority();
        using X509Certificate2 serverCertificate =
            CreateServerCertificate(
                certificateAuthority);
        using X509Certificate2 enrolledClientCertificate =
            CreateClientCertificate(
                certificateAuthority);
        IRuntimeHostCertificateAuthenticationService authenticationService =
            CreateAuthenticationService(
                enrolledClientCertificate,
                enrolledClientCertificate);
        var observationService =
            new TestObservationService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    serverCertificate),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService,
                authenticationService,
                new FixedTimeProvider(
                    AuthenticationTimeUtc));

        await application.StartAsync();

        try
        {
            using var handler =
                new SocketsHttpHandler();
            handler.SslOptions.EnabledSslProtocols =
                SslProtocols.Tls12
                | SslProtocols.Tls13;
            handler.SslOptions.RemoteCertificateValidationCallback =
                static (_, _, _, _) => true;

            using GrpcChannel channel =
                GrpcChannel.ForAddress(
                    GetListeningAddress(
                        application),
                    new GrpcChannelOptions
                    {
                        HttpHandler =
                            handler
                    });
            var client =
                new GrpcV1.RuntimeHostRemoteApi
                    .RuntimeHostRemoteApiClient(
                        channel);
            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            RpcException exception =
                await Assert.ThrowsAsync<RpcException>(
                    () =>
                        call.ResponseStream.MoveNext(
                            CancellationToken.None));

            Assert.Equal(
                StatusCode.Unavailable,
                exception.StatusCode);
            Assert.Equal(
                0,
                observationService.OpenCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task Observe_UnenrolledClientCertificate_ShouldRejectBeforeSubscription()
    {
        using X509Certificate2 certificateAuthority =
            CreateCertificateAuthority();
        using X509Certificate2 serverCertificate =
            CreateServerCertificate(
                certificateAuthority);
        using X509Certificate2 enrolledClientCertificate =
            CreateClientCertificate(
                certificateAuthority);
        using X509Certificate2 unenrolledClientCertificate =
            CreateClientCertificate(
                certificateAuthority);
        IRuntimeHostCertificateAuthenticationService authenticationService =
            CreateAuthenticationService(
                enrolledClientCertificate,
                unenrolledClientCertificate);
        var observationService =
            new TestObservationService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    serverCertificate),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService,
                authenticationService,
                new FixedTimeProvider(
                    AuthenticationTimeUtc));

        await application.StartAsync();

        try
        {
            using var handler =
                new SocketsHttpHandler();
            handler.SslOptions.EnabledSslProtocols =
                SslProtocols.Tls12
                | SslProtocols.Tls13;
            handler.SslOptions.RemoteCertificateValidationCallback =
                static (_, _, _, _) => true;
            handler.SslOptions.LocalCertificateSelectionCallback =
                (_, _, _, _, _) =>
                    unenrolledClientCertificate;

            using GrpcChannel channel =
                GrpcChannel.ForAddress(
                    GetListeningAddress(
                        application),
                    new GrpcChannelOptions
                    {
                        HttpHandler =
                            handler
                    });
            var client =
                new GrpcV1.RuntimeHostRemoteApi
                    .RuntimeHostRemoteApiClient(
                        channel);
            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            RpcException exception =
                await Assert.ThrowsAsync<RpcException>(
                    () =>
                        call.ResponseStream.MoveNext(
                            CancellationToken.None));

            Assert.Equal(
                StatusCode.Unauthenticated,
                exception.StatusCode);
            Assert.Equal(
                0,
                observationService.OpenCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    private static IRuntimeHostCertificateAuthenticationService
        CreateAuthenticationService(
            X509Certificate2 enrolledCertificate,
            X509Certificate2 trustedCertificate)
    {
        var identityExtractor =
            new RuntimeHostX509ClientCredentialIdentityExtractor();
        RuntimeHostClientCredentialIdentity credentialIdentity =
            identityExtractor.Extract(
                enrolledCertificate);
        var enrollmentRegistry =
            new RuntimeHostClientCredentialEnrollmentRegistry(
                new[]
                {
                    new RuntimeHostClientCredentialEnrollment(
                        credentialIdentity,
                        new RuntimeHostClientPrincipalId(
                            "client-01"),
                        "c034-integration-trust-v1")
                });

        return new RuntimeHostCertificateAuthenticationService(
            new RuntimeHostClientCertificateValidator(),
            new RuntimeHostCertificateTrustValidator(
                new ExactCertificateTrustEvaluator(
                    trustedCertificate)),
            identityExtractor,
            new RuntimeHostClientAuthenticationService(
                enrollmentRegistry));
    }

    private static Uri GetListeningAddress(
        WebApplication application)
    {
        IServer server =
            application.Services.GetRequiredService<IServer>();
        IServerAddressesFeature addressesFeature =
            server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException(
                "The server addresses feature is unavailable.");

        return new Uri(
            Assert.Single(
                addressesFeature.Addresses));
    }

    private static X509Certificate2 CreateCertificateAuthority()
    {
        using RSA rsa =
            RSA.Create(
                3072);
        CertificateRequest request =
            new(
                "CN=HASE C-034 Integration Root",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                true,
                false,
                0,
                true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign
                | X509KeyUsageFlags.CrlSign,
                true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                request.PublicKey,
                false));

        return request.CreateSelfSigned(
            AuthenticationTimeUtc.AddDays(
                -2),
            AuthenticationTimeUtc.AddDays(
                2));
    }

    private static X509Certificate2 CreateServerCertificate(
        X509Certificate2 issuerCertificate)
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            CreateEndEntityRequest(
                "CN=localhost",
                rsa,
                "1.3.6.1.5.5.7.3.1",
                X509KeyUsageFlags.DigitalSignature
                | X509KeyUsageFlags.KeyEncipherment);
        var subjectAlternativeName =
            new SubjectAlternativeNameBuilder();
        subjectAlternativeName.AddDnsName(
            "localhost");
        subjectAlternativeName.AddIpAddress(
            IPAddress.Loopback);
        request.CertificateExtensions.Add(
            subjectAlternativeName.Build());

        return CreateIssuedCertificate(
            request,
            rsa,
            issuerCertificate);
    }

    private static X509Certificate2 CreateClientCertificate(
        X509Certificate2 issuerCertificate)
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            CreateEndEntityRequest(
                "CN=hase-c034-client",
                rsa,
                "1.3.6.1.5.5.7.3.2",
                X509KeyUsageFlags.DigitalSignature);

        return CreateIssuedCertificate(
            request,
            rsa,
            issuerCertificate);
    }

    private static CertificateRequest CreateEndEntityRequest(
        string subjectName,
        RSA rsa,
        string enhancedKeyUsage,
        X509KeyUsageFlags keyUsage)
    {
        CertificateRequest request =
            new(
                subjectName,
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
                keyUsage,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new(
                        enhancedKeyUsage)
                },
                true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                request.PublicKey,
                false));

        return request;
    }

    private static X509Certificate2 CreateIssuedCertificate(
        CertificateRequest request,
        RSA privateKey,
        X509Certificate2 issuerCertificate)
    {
        byte[] serialNumber =
            RandomNumberGenerator.GetBytes(
                16);

        using X509Certificate2 publicCertificate =
            request.Create(
                issuerCertificate,
                AuthenticationTimeUtc.AddDays(
                    -1),
                AuthenticationTimeUtc.AddDays(
                    1),
                serialNumber);
        using X509Certificate2 certificateWithPrivateKey =
            publicCertificate.CopyWithPrivateKey(
                privateKey);

        const string password =
            "hase-c034-secure-integration";
        byte[] pkcs12 =
            certificateWithPrivateKey.Export(
                X509ContentType.Pkcs12,
                password);

        return X509CertificateLoader.LoadPkcs12(
            pkcs12,
            password);
    }

    private sealed class ExactCertificateTrustEvaluator
        : IRuntimeHostCertificateTrustEvaluator
    {
        private readonly string trustedThumbprint;

        public ExactCertificateTrustEvaluator(
            X509Certificate2 trustedCertificate)
        {
            trustedThumbprint =
                trustedCertificate.Thumbprint;
        }

        public bool IsTrusted(
            X509Certificate2 certificate,
            DateTimeOffset validationTimeUtc)
        {
            return string.Equals(
                trustedThumbprint,
                certificate.Thumbprint,
                StringComparison.OrdinalIgnoreCase);
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

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-c034-snapshot"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TestObservationService
        : Northbound.IRuntimeHostObservationService
    {
        public TestObservationService()
        {
            var snapshot =
                new Northbound.PublishedRuntimeHostSnapshot(
                    new Northbound.RuntimeHostId(
                        "runtime-host-c034-secure-observation"),
                    Northbound.RuntimeHostApiVersion.Current,
                    Array.Empty<
                        Northbound.PublishedRuntimeEndpointSnapshot>());
            var observation =
                new Northbound.RuntimeHostObservation(
                    new Northbound.RuntimeHostObservationSequence(
                        1),
                    new Hase.Core.Domain.Identity.EndpointId(
                        "doit-esp32-devkitc-v4-01"),
                    new Northbound.RuntimeEndpointAttachmentGeneration(
                        new Guid(
                            "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
                    new Northbound.RuntimeHostEventOccurredObservationPayload(
                        new Hase.Core.Domain.Identity.InstrumentId(
                            "controller-01"),
                        new Hase.Core.Domain.Properties.DescriptorPath(
                            "Controller",
                            "ButtonPressed"),
                        DateTimeOffset.UnixEpoch,
                        "pressed"));

            Subscription =
                new TestObservationSubscription(
                    snapshot,
                    observation);
        }

        public int OpenCount
        {
            get;
            private set;
        }

        public CancellationToken CancellationToken
        {
            get;
            private set;
        }

        public TestObservationSubscription Subscription
        {
            get;
        }

        public Task<Northbound.RuntimeHostObservationSubscription>
            OpenSubscriptionAsync(
                Northbound.RuntimeHostObservationSubscriptionOptions options,
                CancellationToken cancellationToken = default)
        {
            OpenCount++;
            CancellationToken =
                cancellationToken;

            return Task.FromResult<
                Northbound.RuntimeHostObservationSubscription>(
                    Subscription);
        }
    }

    private sealed class TestObservationSubscription
        : Northbound.RuntimeHostObservationSubscription
    {
        private readonly Northbound.RuntimeHostObservation observation;

        public TestObservationSubscription(
            Northbound.PublishedRuntimeHostSnapshot initialSnapshot,
            Northbound.RuntimeHostObservation observation)
            : base(
                initialSnapshot,
                new Northbound.RuntimeHostObservationSequence(
                    0))
        {
            this.observation =
                observation;
        }

        public int DisposeCount
        {
            get;
            private set;
        }

        public override async IAsyncEnumerable<
            Northbound.RuntimeHostObservation> ReadAllAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return observation;

            await Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }
}
