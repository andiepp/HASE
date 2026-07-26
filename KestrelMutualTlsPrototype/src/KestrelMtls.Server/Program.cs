using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using KestrelMtls.Server;

const string certificatePassword = "hase-prototype";

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: KestrelMtls.Server <certificate-directory>");
    return 1;
}

var certificateDirectory = Path.GetFullPath(args[0]);
var serverCertificatePath = Path.Combine(certificateDirectory, "server.pfx");
var rootCertificatePath = Path.Combine(certificateDirectory, "root.cer");

using var serverCertificate = X509CertificateLoader.LoadPkcs12FromFile(
    serverCertificatePath,
    certificatePassword);
using var trustedRoot = X509CertificateLoader.LoadCertificateFromFile(
    rootCertificatePath);

var builder = WebApplication.CreateBuilder(args);
var probeExecutionCount = 0;

builder.Services.AddGrpc();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, 7443, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = serverCertificate;
            httpsOptions.ClientCertificateMode =
                ClientCertificateMode.RequireCertificate;
            httpsOptions.ClientCertificateValidation =
                (certificate, _, _) => ValidateClientCertificate(
                    certificate,
                    trustedRoot);
        });
    });
});

var app = builder.Build();

app.MapGrpcService<ProbeGrpcService>();

app.MapGet(
    "/probe",
    (HttpContext context) =>
    {
        var executionCount = Interlocked.Increment(
            ref probeExecutionCount);
        Console.WriteLine(
            $"Probe endpoint execution count: {executionCount}");

        var clientCertificate =
            context.Connection.ClientCertificate
            ?? throw new InvalidOperationException(
                "The TLS connection has no client certificate.");

        return Results.Ok(
            new ProbeResponse(
                Authenticated: true,
                ClientSubject: clientCertificate.Subject,
                Protocol: context.Request.Protocol));
    });

app.Lifetime.ApplicationStarted.Register(
    () => Console.WriteLine(
        "P-001 server ready: https://localhost:7443"));

await app.RunAsync();
return 0;

static bool ValidateClientCertificate(
    X509Certificate2 certificate,
    X509Certificate2 trustedRoot)
{
    using var chain = new X509Chain();
    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
    chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
    chain.ChainPolicy.ApplicationPolicy.Add(
        new Oid("1.3.6.1.5.5.7.3.2"));

    var valid = chain.Build(certificate);
    var status = chain.ChainStatus.Length == 0
        ? "NoError"
        : string.Join(
            ", ",
            chain.ChainStatus.Select(item => item.Status));

    if (valid)
    {
        Console.WriteLine(
            $"Accepted client certificate: {certificate.Subject}");
    }
    else
    {
        Console.WriteLine(
            $"Rejected client certificate: {certificate.Subject}; {status}");
    }

    return valid;
}

internal sealed record ProbeResponse(
    bool Authenticated,
    string ClientSubject,
    string Protocol);
