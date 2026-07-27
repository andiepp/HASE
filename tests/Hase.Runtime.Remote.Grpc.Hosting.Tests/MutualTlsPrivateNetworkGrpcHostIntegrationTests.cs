using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.AspNetCore.Builder;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class MutualTlsPrivateNetworkGrpcHostIntegrationTests
{
    [Fact]
    public async Task GetSnapshot_EnrolledClientOverPrivateNetwork_ShouldSucceed()
    {
        IPAddress listenerAddress =
            GetPrivateNetworkAddress();
        int listenerPort =
            ReserveAvailablePort(
                listenerAddress);
        DateTimeOffset authenticationTimeUtc =
            DateTimeOffset.UtcNow;

        using X509Certificate2 certificateAuthority =
            CreateCertificateAuthority(
                authenticationTimeUtc);
        using X509Certificate2 serverCertificate =
            CreateServerCertificate(
                certificateAuthority,
                listenerAddress,
                authenticationTimeUtc);
        using X509Certificate2 clientCertificate =
            CreateClientCertificate(
                certificateAuthority,
                authenticationTimeUtc);
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
                            "private-network-client"),
                        "private-network-integration-trust-v1")
                });
        var authenticationService =
            new RuntimeHostCertificateAuthenticationService(
                new RuntimeHostClientCertificateValidator(),
                new RuntimeHostCertificateTrustValidator(
                    new ExactCertificateTrustEvaluator(
                        clientCertificate)),
                identityExtractor,
                new RuntimeHostClientAuthenticationService(
                    enrollmentRegistry));
        var snapshotProvider =
            new TrackingSnapshotProvider();

        await using WebApplication application =
            MutualTlsPrivateNetworkGrpcHostFactory.Create(
                new PrivateNetworkGrpcBinding(
                    listenerAddress,
                    listenerPort),
                RuntimeHostMutualTlsOptions.EnabledWith(
                    serverCertificate),
                snapshotProvider,
                authenticationService,
                new FixedTimeProvider(
                    authenticationTimeUtc));

        await application.StartAsync();

        try
        {
            using RuntimeHostPrivateNetworkGrpcClient client =
                RuntimeHostPrivateNetworkGrpcClient.Create(
                    new Uri(
                        $"https://{FormatHost(listenerAddress)}:"
                        + listenerPort),
                    clientCertificate,
                    serverCertificate);

            GrpcV1.GetSnapshotResponse response =
                await client.Client.GetSnapshotAsync(
                    new GrpcV1.GetSnapshotRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            Assert.True(
                snapshotProvider.WasCalled);
            Assert.Equal(
                "runtime-host-private-network-integration",
                response.RuntimeHostId);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    private static IPAddress GetPrivateNetworkAddress()
    {
        IPAddress? address =
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(
                    networkInterface =>
                        networkInterface.OperationalStatus
                            == OperationalStatus.Up
                        && networkInterface.NetworkInterfaceType
                            != NetworkInterfaceType.Loopback)
                .SelectMany(
                    networkInterface =>
                        networkInterface.GetIPProperties()
                            .UnicastAddresses)
                .Select(
                    addressInformation =>
                        addressInformation.Address)
                .FirstOrDefault(
                    candidate =>
                        candidate.AddressFamily
                            == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(
                            candidate)
                        && !candidate.Equals(
                            IPAddress.Any));

        return address
            ?? throw new InvalidOperationException(
                "No operational non-loopback IPv4 interface is available "
                + "for private-network integration validation.");
    }

    private static int ReserveAvailablePort(
        IPAddress address)
    {
        var listener =
            new TcpListener(
                address,
                0);

        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string FormatHost(
        IPAddress address)
    {
        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{address}]"
            : address.ToString();
    }

    private static X509Certificate2 CreateCertificateAuthority(
        DateTimeOffset authenticationTimeUtc)
    {
        using RSA rsa =
            RSA.Create(
                3072);
        CertificateRequest request =
            new(
                "CN=HASE generated private-network integration root",
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
            authenticationTimeUtc.AddMinutes(
                -5),
            authenticationTimeUtc.AddMinutes(
                30));
    }

    private static X509Certificate2 CreateServerCertificate(
        X509Certificate2 issuerCertificate,
        IPAddress listenerAddress,
        DateTimeOffset authenticationTimeUtc)
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            CreateEndEntityRequest(
                "CN=HASE generated private-network integration server",
                rsa,
                "1.3.6.1.5.5.7.3.1",
                X509KeyUsageFlags.DigitalSignature
                | X509KeyUsageFlags.KeyEncipherment);
        var subjectAlternativeName =
            new SubjectAlternativeNameBuilder();
        subjectAlternativeName.AddIpAddress(
            listenerAddress);
        request.CertificateExtensions.Add(
            subjectAlternativeName.Build());

        return CreateIssuedCertificate(
            request,
            rsa,
            issuerCertificate,
            authenticationTimeUtc);
    }

    private static X509Certificate2 CreateClientCertificate(
        X509Certificate2 issuerCertificate,
        DateTimeOffset authenticationTimeUtc)
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            CreateEndEntityRequest(
                "CN=HASE generated private-network integration client",
                rsa,
                "1.3.6.1.5.5.7.3.2",
                X509KeyUsageFlags.DigitalSignature);

        return CreateIssuedCertificate(
            request,
            rsa,
            issuerCertificate,
            authenticationTimeUtc);
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

        return request;
    }

    private static X509Certificate2 CreateIssuedCertificate(
        CertificateRequest request,
        RSA privateKey,
        X509Certificate2 issuerCertificate,
        DateTimeOffset authenticationTimeUtc)
    {
        byte[] serialNumber =
            RandomNumberGenerator.GetBytes(
                16);

        using X509Certificate2 publicCertificate =
            request.Create(
                issuerCertificate,
                authenticationTimeUtc.AddMinutes(
                    -1),
                authenticationTimeUtc.AddMinutes(
                    10),
                serialNumber);
        using X509Certificate2 certificateWithPrivateKey =
            publicCertificate.CopyWithPrivateKey(
                privateKey);
        string exportPassword =
            Convert.ToHexString(
                RandomNumberGenerator.GetBytes(
                    16));
        byte[] pkcs12 =
            certificateWithPrivateKey.Export(
                X509ContentType.Pkcs12,
                exportPassword);

        return X509CertificateLoader.LoadPkcs12(
            pkcs12,
            exportPassword);
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

    private sealed class TrackingSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public bool WasCalled
        {
            get;
            private set;
        }

        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            WasCalled =
                true;

            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-private-network-integration"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }
}
