using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Owns the isolated certificates used by one C-032 physical validation run.
/// </summary>
internal sealed class CapabilityC032CertificateSet
    : IDisposable
{
    private const string ServerAuthenticationOid =
        "1.3.6.1.5.5.7.3.1";

    private const string ClientAuthenticationOid =
        "1.3.6.1.5.5.7.3.2";

    private bool disposed;

    private CapabilityC032CertificateSet(
        X509Certificate2 certificateAuthority,
        X509Certificate2 serverCertificate,
        X509Certificate2 clientCertificate)
    {
        CertificateAuthority =
            certificateAuthority;

        ServerCertificate =
            serverCertificate;

        ClientCertificate =
            clientCertificate;
    }

    /// <summary>
    /// Gets the isolated certification authority.
    /// </summary>
    public X509Certificate2 CertificateAuthority
    {
        get;
    }

    /// <summary>
    /// Gets the CA-issued localhost server certificate.
    /// </summary>
    public X509Certificate2 ServerCertificate
    {
        get;
    }

    /// <summary>
    /// Gets the CA-issued enrolled client certificate.
    /// </summary>
    public X509Certificate2 ClientCertificate
    {
        get;
    }

    /// <summary>
    /// Creates an isolated certificate set valid around the supplied time.
    /// </summary>
    public static CapabilityC032CertificateSet Create(
        DateTimeOffset validationTimeUtc)
    {
        using RSA certificateAuthorityKey =
            RSA.Create(
                3072);
        CertificateRequest certificateAuthorityRequest =
            new(
                "CN=HASE C-032 Physical Validation Root",
                certificateAuthorityKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        certificateAuthorityRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                true,
                false,
                0,
                true));
        certificateAuthorityRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign
                | X509KeyUsageFlags.CrlSign,
                true));
        certificateAuthorityRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                certificateAuthorityRequest.PublicKey,
                false));

        X509Certificate2? certificateAuthority =
            null;
        X509Certificate2? serverCertificate =
            null;
        X509Certificate2? clientCertificate =
            null;

        try
        {
            certificateAuthority =
                certificateAuthorityRequest.CreateSelfSigned(
                    validationTimeUtc.AddDays(
                        -1),
                    validationTimeUtc.AddDays(
                        1));

            serverCertificate =
                CreateServerCertificate(
                    certificateAuthority,
                    validationTimeUtc);

            clientCertificate =
                CreateClientCertificate(
                    certificateAuthority,
                    validationTimeUtc);

            var result =
                new CapabilityC032CertificateSet(
                    certificateAuthority,
                    serverCertificate,
                    clientCertificate);

            certificateAuthority =
                null;
            serverCertificate =
                null;
            clientCertificate =
                null;

            return result;
        }
        finally
        {
            clientCertificate?.Dispose();
            serverCertificate?.Dispose();
            certificateAuthority?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        ClientCertificate.Dispose();
        ServerCertificate.Dispose();
        CertificateAuthority.Dispose();
    }

    private static X509Certificate2 CreateServerCertificate(
        X509Certificate2 certificateAuthority,
        DateTimeOffset validationTimeUtc)
    {
        using RSA privateKey =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=localhost",
                privateKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        AddEndEntityExtensions(
            request,
            ServerAuthenticationOid,
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
            privateKey,
            certificateAuthority,
            validationTimeUtc,
            "hase-c032-server");
    }

    private static X509Certificate2 CreateClientCertificate(
        X509Certificate2 certificateAuthority,
        DateTimeOffset validationTimeUtc)
    {
        using RSA privateKey =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=hase-c032-client",
                privateKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        AddEndEntityExtensions(
            request,
            ClientAuthenticationOid,
            X509KeyUsageFlags.DigitalSignature);

        return CreateIssuedCertificate(
            request,
            privateKey,
            certificateAuthority,
            validationTimeUtc,
            "hase-c032-client");
    }

    private static void AddEndEntityExtensions(
        CertificateRequest request,
        string enhancedKeyUsageOid,
        X509KeyUsageFlags keyUsage)
    {
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
                        enhancedKeyUsageOid)
                },
                true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                request.PublicKey,
                false));
    }

    private static X509Certificate2 CreateIssuedCertificate(
        CertificateRequest request,
        RSA privateKey,
        X509Certificate2 certificateAuthority,
        DateTimeOffset validationTimeUtc,
        string password)
    {
        byte[] serialNumber =
            RandomNumberGenerator.GetBytes(
                16);

        using X509Certificate2 publicCertificate =
            request.Create(
                certificateAuthority,
                validationTimeUtc.AddHours(
                    -1),
                validationTimeUtc.AddHours(
                    8),
                serialNumber);
        using X509Certificate2 certificateWithPrivateKey =
            publicCertificate.CopyWithPrivateKey(
                privateKey);
        byte[] pkcs12 =
            certificateWithPrivateKey.Export(
                X509ContentType.Pkcs12,
                password);

        return X509CertificateLoader.LoadPkcs12(
            pkcs12,
            password);
    }
}
