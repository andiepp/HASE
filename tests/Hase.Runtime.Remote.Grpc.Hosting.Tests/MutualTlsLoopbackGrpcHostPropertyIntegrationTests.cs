using System.Net;
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

public sealed class MutualTlsLoopbackGrpcHostPropertyIntegrationTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            10,
            0,
            0,
            TimeSpan.Zero);

    private static readonly Guid AttachmentGeneration =
        new(
            "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd");

    [Fact]
    public async Task ReadAuthoritativeProperty_EnrolledClientCertificate_ShouldReachService()
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
                        "integration-trust-v1")
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
        var propertyService =
            new TrackingPropertyService();
        bool? projectedIdentityAuthenticated =
            null;
        string? projectedIdentityName =
            null;

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    serverCertificate),
                new TestSnapshotProvider(),
                propertyService,
                certificateAuthenticationService,
                new FixedTimeProvider(
                    AuthenticationTimeUtc));

        application.Use(
            async (httpContext, next) =>
            {
                projectedIdentityAuthenticated =
                    httpContext.User.Identity?.IsAuthenticated;
                projectedIdentityName =
                    httpContext.User.Identity?.Name;

                await next();
            });

        await application.StartAsync();

        try
        {
            Uri address =
                GetListeningAddress(
                    application);

            using var handler =
                new HttpClientHandler
                {
                    ClientCertificateOptions =
                        ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback =
                        static (_, _, _, _) => true
                };
            handler.ClientCertificates.Add(
                clientCertificate);

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

            GrpcV1.PropertyOperationResult response =
                await client.ReadAuthoritativePropertyAsync(
                    new GrpcV1.ReadAuthoritativePropertyRequest
                    {
                        Target =
                            new GrpcV1.PropertyTarget
                            {
                                EndpointId =
                                    "endpoint-01",
                                AttachmentGeneration =
                                    AttachmentGeneration.ToString(),
                                InstrumentId =
                                    "environment-sensor-01",
                                PropertyId =
                                    "physical.environment-sensor.temperature"
                            }
                    },
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            Assert.True(
                projectedIdentityAuthenticated);
            Assert.Equal(
                "client-01",
                projectedIdentityName);
            Assert.Equal(
                1,
                propertyService.ReadCount);
            Assert.Null(
                propertyService.CachedTarget);
            Assert.NotNull(
                propertyService.ReadTarget);
            Assert.Equal(
                new Hase.Core.Domain.Identity.EndpointId(
                    "endpoint-01"),
                propertyService.ReadTarget.EndpointId);
            Assert.Equal(
                AttachmentGeneration,
                propertyService.ReadTarget
                    .AttachmentGeneration.Value);
            Assert.Equal(
                new Hase.Core.Domain.Identity.InstrumentId(
                    "environment-sensor-01"),
                propertyService.ReadTarget.InstrumentId);
            Assert.Equal(
                new Hase.Core.Domain.Identity.PropertyId(
                    "physical.environment-sensor.temperature"),
                propertyService.ReadTarget.PropertyId);
            Assert.True(
                propertyService.ReadCancellationToken.CanBeCanceled);
            Assert.Equal(
                GrpcV1.PropertyOperationStatus.Success,
                response.Status);
            Assert.NotNull(
                response.ConfirmedValue);
            Assert.Equal(
                GrpcV1.PropertyQuality.Good,
                response.ConfirmedValue.Quality);
            Assert.Equal(
                23.75,
                response.ConfirmedValue.Value.NumericValue);
            Assert.Equal(
                DateTimeOffset.UnixEpoch,
                response.ConfirmedValue.TimestampUtc.ToDateTimeOffset());
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task ReadAuthoritativeProperty_UnenrolledClientCertificate_ShouldRejectBeforeService()
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

        var identityExtractor =
            new RuntimeHostX509ClientCredentialIdentityExtractor();
        RuntimeHostClientCredentialIdentity enrolledCredentialIdentity =
            identityExtractor.Extract(
                enrolledClientCertificate);
        var enrollmentRegistry =
            new RuntimeHostClientCredentialEnrollmentRegistry(
                new[]
                {
                    new RuntimeHostClientCredentialEnrollment(
                        enrolledCredentialIdentity,
                        new RuntimeHostClientPrincipalId(
                            "client-01"),
                        "integration-trust-v1")
                });
        var certificateAuthenticationService =
            new RuntimeHostCertificateAuthenticationService(
                new RuntimeHostClientCertificateValidator(),
                new RuntimeHostCertificateTrustValidator(
                    new ExactCertificateTrustEvaluator(
                        unenrolledClientCertificate)),
                identityExtractor,
                new RuntimeHostClientAuthenticationService(
                    enrollmentRegistry));
        var propertyService =
            new TrackingPropertyService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    serverCertificate),
                new TestSnapshotProvider(),
                propertyService,
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
                new HttpClientHandler
                {
                    ClientCertificateOptions =
                        ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback =
                        static (_, _, _, _) => true
                };
            handler.ClientCertificates.Add(
                unenrolledClientCertificate);

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

            RpcException exception =
                await Assert.ThrowsAsync<RpcException>(
                    async () =>
                    {
                        await client.ReadAuthoritativePropertyAsync(
                            new GrpcV1.ReadAuthoritativePropertyRequest
                            {
                                Target =
                                    new GrpcV1.PropertyTarget
                                    {
                                        EndpointId =
                                            "endpoint-01",
                                        AttachmentGeneration =
                                            AttachmentGeneration.ToString(),
                                        InstrumentId =
                                            "environment-sensor-01",
                                        PropertyId =
                                            "physical.environment-sensor.temperature"
                                    }
                            },
                            deadline:
                                DateTime.UtcNow.AddSeconds(
                                    10));
                    });

            Assert.Equal(
                StatusCode.Unauthenticated,
                exception.StatusCode);
            Assert.Equal(
                0,
                propertyService.ReadCount);
            Assert.Null(
                propertyService.CachedTarget);
            Assert.Null(
                propertyService.ReadTarget);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task ReadAuthoritativeProperty_MissingClientCertificate_ShouldRejectBeforeService()
    {
        using X509Certificate2 certificateAuthority =
            CreateCertificateAuthority();
        using X509Certificate2 serverCertificate =
            CreateServerCertificate(
                certificateAuthority);
        using X509Certificate2 enrolledClientCertificate =
            CreateClientCertificate(
                certificateAuthority);

        var identityExtractor =
            new RuntimeHostX509ClientCredentialIdentityExtractor();
        RuntimeHostClientCredentialIdentity enrolledCredentialIdentity =
            identityExtractor.Extract(
                enrolledClientCertificate);
        var enrollmentRegistry =
            new RuntimeHostClientCredentialEnrollmentRegistry(
                new[]
                {
                    new RuntimeHostClientCredentialEnrollment(
                        enrolledCredentialIdentity,
                        new RuntimeHostClientPrincipalId(
                            "client-01"),
                        "integration-trust-v1")
                });
        var certificateAuthenticationService =
            new RuntimeHostCertificateAuthenticationService(
                new RuntimeHostClientCertificateValidator(),
                new RuntimeHostCertificateTrustValidator(
                    new ExactCertificateTrustEvaluator(
                        enrolledClientCertificate)),
                identityExtractor,
                new RuntimeHostClientAuthenticationService(
                    enrollmentRegistry));
        var propertyService =
            new TrackingPropertyService();

        await using WebApplication application =
            MutualTlsLoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    serverCertificate),
                new TestSnapshotProvider(),
                propertyService,
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
                new HttpClientHandler
                {
                    ClientCertificateOptions =
                        ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback =
                        static (_, _, _, _) => true
                };

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

            RpcException exception =
                await Assert.ThrowsAsync<RpcException>(
                    async () =>
                    {
                        await client.ReadAuthoritativePropertyAsync(
                            new GrpcV1.ReadAuthoritativePropertyRequest
                            {
                                Target =
                                    new GrpcV1.PropertyTarget
                                    {
                                        EndpointId =
                                            "endpoint-01",
                                        AttachmentGeneration =
                                            AttachmentGeneration.ToString(),
                                        InstrumentId =
                                            "environment-sensor-01",
                                        PropertyId =
                                            "physical.environment-sensor.temperature"
                                    }
                            },
                            deadline:
                                DateTime.UtcNow.AddSeconds(
                                    10));
                    });

            Assert.Equal(
                StatusCode.Unavailable,
                exception.StatusCode);
            Assert.Equal(
                0,
                propertyService.ReadCount);
            Assert.Null(
                propertyService.CachedTarget);
            Assert.Null(
                propertyService.ReadTarget);
        }
        finally
        {
            await application.StopAsync();
        }
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
                "CN=HASE Secure Property Integration Root",
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
            new(
                "CN=localhost",
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
                X509KeyUsageFlags.DigitalSignature
                | X509KeyUsageFlags.KeyEncipherment,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new(
                        "1.3.6.1.5.5.7.3.1")
                },
                true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                request.PublicKey,
                false));

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
            new(
                "CN=hase-property-client",
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
                    new(
                        "1.3.6.1.5.5.7.3.2")
                },
                true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                request.PublicKey,
                false));

        return CreateIssuedCertificate(
            request,
            rsa,
            issuerCertificate);
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
            "hase-secure-property-integration";
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

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-secure-property-integration"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TrackingPropertyService
        : Northbound.IRuntimeHostPropertyService
    {
        public int ReadCount
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostPropertyTarget? CachedTarget
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostPropertyTarget? ReadTarget
        {
            get;
            private set;
        }

        public CancellationToken ReadCancellationToken
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostCachedPropertyResult GetCached(
            Northbound.RuntimeHostPropertyTarget target)
        {
            CachedTarget =
                target;

            throw new InvalidOperationException(
                "The authoritative Property RPC used the cached path.");
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> ReadAsync(
            Northbound.RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            ReadTarget =
                target;
            ReadCancellationToken =
                cancellationToken;

            return Task.FromResult(
                Northbound.RuntimeHostPropertyOperationResult.Successful(
                    new Hase.Core.Domain.Properties.PropertyValue(
                        23.75,
                        DateTimeOffset.UnixEpoch)));
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> WriteAsync(
            Northbound.RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The authoritative Property read used the write path.");
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
