using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

const string certificatePassword = "hase-prototype";

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: KestrelMtls.Certificates <certificate-output-directory>");
    return 1;
}

var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);

var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
var rootNotAfter = notBefore.AddYears(5);
var leafNotAfter = notBefore.AddYears(1);

using var rootCertificate = CreateRootCertificate(
    "CN=HASE Kestrel Prototype Root",
    notBefore,
    rootNotAfter);
using var untrustedRootCertificate = CreateRootCertificate(
    "CN=HASE Kestrel Prototype Untrusted Root",
    notBefore,
    rootNotAfter);

using var serverCertificate = CreateLeafCertificate(
    rootCertificate,
    "CN=localhost",
    "1.3.6.1.5.5.7.3.1",
    leafNotAfter,
    configureRequest: request =>
    {
        var subjectAlternativeName = new SubjectAlternativeNameBuilder();
        subjectAlternativeName.AddDnsName("localhost");
        subjectAlternativeName.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeName.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeName.Build());
    });

using var clientCertificate = CreateLeafCertificate(
    rootCertificate,
    "CN=HASE Kestrel Prototype Client",
    "1.3.6.1.5.5.7.3.2",
    leafNotAfter);

using var untrustedClientCertificate = CreateLeafCertificate(
    untrustedRootCertificate,
    "CN=HASE Kestrel Prototype Untrusted Client",
    "1.3.6.1.5.5.7.3.2",
    leafNotAfter);

WriteCertificate(
    Path.Combine(outputDirectory, "root.cer"),
    rootCertificate.Export(X509ContentType.Cert));
WriteCertificate(
    Path.Combine(outputDirectory, "server.pfx"),
    serverCertificate.Export(X509ContentType.Pfx, certificatePassword));
WriteCertificate(
    Path.Combine(outputDirectory, "client.pfx"),
    clientCertificate.Export(X509ContentType.Pfx, certificatePassword));
WriteCertificate(
    Path.Combine(outputDirectory, "untrusted-client.pfx"),
    untrustedClientCertificate.Export(
        X509ContentType.Pfx,
        certificatePassword));

Console.WriteLine($"Certificate directory : {outputDirectory}");
Console.WriteLine($"Root thumbprint       : {rootCertificate.Thumbprint}");
Console.WriteLine($"Server thumbprint     : {serverCertificate.Thumbprint}");
Console.WriteLine($"Client thumbprint     : {clientCertificate.Thumbprint}");
Console.WriteLine(
    $"Untrusted thumbprint  : {untrustedClientCertificate.Thumbprint}");

return 0;

static X509Certificate2 CreateRootCertificate(
    string subjectName,
    DateTimeOffset notBefore,
    DateTimeOffset notAfter)
{
    using var rootKey = RSA.Create(3072);
    var request = new CertificateRequest(
        subjectName,
        rootKey,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    request.CertificateExtensions.Add(
        new X509BasicConstraintsExtension(true, false, 0, true));
    request.CertificateExtensions.Add(
        new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
    request.CertificateExtensions.Add(
        new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

    return request.CreateSelfSigned(notBefore, notAfter);
}

static X509Certificate2 CreateLeafCertificate(
    X509Certificate2 issuerCertificate,
    string subjectName,
    string enhancedKeyUsageOid,
    DateTimeOffset notAfter,
    Action<CertificateRequest>? configureRequest = null)
{
    using var leafKey = RSA.Create(2048);
    var request = new CertificateRequest(
        subjectName,
        leafKey,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    request.CertificateExtensions.Add(
        new X509BasicConstraintsExtension(false, false, 0, true));
    request.CertificateExtensions.Add(
        new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
    request.CertificateExtensions.Add(
        new X509EnhancedKeyUsageExtension(
            new OidCollection
            {
                new(enhancedKeyUsageOid),
            },
            true));
    request.CertificateExtensions.Add(
        new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

    configureRequest?.Invoke(request);

    var serialNumber = RandomNumberGenerator.GetBytes(16);
    using var publicCertificate = request.Create(
        issuerCertificate,
        DateTimeOffset.UtcNow.AddMinutes(-5),
        notAfter,
        serialNumber);

    return publicCertificate.CopyWithPrivateKey(leafKey);
}

static void WriteCertificate(string path, byte[] content)
{
    File.WriteAllBytes(path, content);
    Console.WriteLine($"Created               : {path}");
}
