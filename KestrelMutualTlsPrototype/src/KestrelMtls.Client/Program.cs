using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
using KestrelMtls.Grpc;

const string certificatePassword = "hase-prototype";
const string authenticatedMode = "authenticated";
const string missingCertificateMode = "missing";
const string untrustedCertificateMode = "untrusted";
const string grpcMode = "grpc";

if (args.Length != 2
    || (args[1] != authenticatedMode
        && args[1] != missingCertificateMode
        && args[1] != untrustedCertificateMode
        && args[1] != grpcMode))
{
    Console.Error.WriteLine(
        "Usage: KestrelMtls.Client <certificate-directory> "
        + "<authenticated|missing|untrusted|grpc>");
    return 1;
}

var certificateDirectory = Path.GetFullPath(args[0]);
var mode = args[1];
var clientCertificatePath = Path.Combine(
    certificateDirectory,
    "client.pfx");
var untrustedClientCertificatePath = Path.Combine(
    certificateDirectory,
    "untrusted-client.pfx");
var rootCertificatePath = Path.Combine(
    certificateDirectory,
    "root.cer");

using var trustedRoot = X509CertificateLoader.LoadCertificateFromFile(
    rootCertificatePath);
using var handler = new HttpClientHandler
{
    ClientCertificateOptions = ClientCertificateOption.Manual,
};

X509Certificate2? clientCertificate = null;

if (mode != missingCertificateMode)
{
    clientCertificate = X509CertificateLoader.LoadPkcs12FromFile(
        mode == untrustedCertificateMode
            ? untrustedClientCertificatePath
            : clientCertificatePath,
        certificatePassword);
    handler.ClientCertificates.Add(clientCertificate);
}

handler.ServerCertificateCustomValidationCallback =
    (_, certificate, _, _) =>
        certificate is not null
        && ValidateServerCertificate(
            certificate,
            trustedRoot);

using (clientCertificate)
using (var client = new HttpClient(handler)
{
    BaseAddress = new Uri("https://localhost:7443"),
    Timeout = TimeSpan.FromSeconds(10),
    DefaultRequestVersion = HttpVersion.Version20,
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
})
{
    return mode switch
    {
        authenticatedMode => await RunAuthenticatedAsync(client),
        missingCertificateMode =>
            await RunRejectedAsync(
                client,
                "P-002",
                "No client certificate was supplied."),
        untrustedCertificateMode =>
            await RunRejectedAsync(
                client,
                "P-003",
                "An untrusted client certificate was supplied."),
        grpcMode => await RunGrpcAsync(handler),
        _ => throw new InvalidOperationException(
            $"Unsupported client mode '{mode}'."),
    };
}

static async Task<int> RunGrpcAsync(HttpMessageHandler handler)
{
    try
    {
        using var channel = GrpcChannel.ForAddress(
            "https://localhost:7443",
            new GrpcChannelOptions
            {
                HttpHandler = handler,
                DisposeHttpClient = false,
            });
        var client = new GrpcProbe.GrpcProbeClient(channel);
        var reply = await client.ProbeAsync(new ProbeRequest());

        Console.WriteLine(
            $"Authenticated     : {reply.Authenticated}");
        Console.WriteLine(
            $"Client subject    : {reply.ClientSubject}");
        Console.WriteLine(
            $"Server protocol   : {reply.Protocol}");

        var passed =
            reply.Authenticated
            && reply.Protocol == "HTTP/2"
            && reply.ClientSubject
                == "CN=HASE Kestrel Prototype Client";

        Console.WriteLine(
            $"P-004 RESULT      : {(passed ? "PASS" : "FAIL")}");
        return passed ? 0 : 6;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(
            $"P-004 RESULT      : FAIL ({exception.GetType().Name})");
        Console.Error.WriteLine(exception);
        return 7;
    }
}

static async Task<int> RunAuthenticatedAsync(HttpClient client)
{
    try
    {
        using var response = await client.GetAsync("/probe");
        var probe = await response.Content.ReadFromJsonAsync<ProbeResponse>();

        Console.WriteLine(
            $"HTTP status       : {(int)response.StatusCode} "
            + response.StatusCode);
        Console.WriteLine($"HTTP version      : {response.Version}");
        Console.WriteLine(
            $"Authenticated     : {probe?.Authenticated}");
        Console.WriteLine(
            $"Client subject    : {probe?.ClientSubject}");
        Console.WriteLine(
            $"Server protocol   : {probe?.Protocol}");

        var passed =
            response.StatusCode == HttpStatusCode.OK
            && response.Version.Major == 2
            && probe is
            {
                Authenticated: true,
                Protocol: "HTTP/2",
                ClientSubject:
                    "CN=HASE Kestrel Prototype Client",
            };

        Console.WriteLine(
            $"P-001 RESULT      : {(passed ? "PASS" : "FAIL")}");
        return passed ? 0 : 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(
            $"P-001 RESULT      : FAIL ({exception.GetType().Name})");
        Console.Error.WriteLine(exception);
        return 3;
    }
}

static async Task<int> RunRejectedAsync(
    HttpClient client,
    string capability,
    string description)
{
    try
    {
        using var response = await client.GetAsync("/probe");
        Console.Error.WriteLine(
            $"Unexpected HTTP response: {(int)response.StatusCode} "
            + response.StatusCode);
        Console.Error.WriteLine(
            $"{capability} RESULT      : FAIL "
            + "(the TLS connection was not rejected)");
        return 4;
    }
    catch (HttpRequestException exception)
    {
        Console.WriteLine(
            $"Expected failure  : {exception.GetType().Name}");
        Console.WriteLine(description);
        Console.WriteLine($"{capability} RESULT      : PASS");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(
            $"{capability} RESULT      : FAIL "
            + $"({exception.GetType().Name})");
        Console.Error.WriteLine(exception);
        return 5;
    }
}

static bool ValidateServerCertificate(
    X509Certificate2 certificate,
    X509Certificate2 trustedRoot)
{
    using var chain = new X509Chain();
    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
    chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
    chain.ChainPolicy.ApplicationPolicy.Add(
        new Oid("1.3.6.1.5.5.7.3.1"));

    return chain.Build(certificate);
}

internal sealed record ProbeResponse(
    bool Authenticated,
    string ClientSubject,
    string Protocol);
