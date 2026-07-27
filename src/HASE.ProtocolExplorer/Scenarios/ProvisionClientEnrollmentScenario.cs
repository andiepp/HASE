using System.Security.Cryptography.X509Certificates;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Creates public HASE client-enrollment metadata from an externally issued
/// public client certificate.
/// </summary>
internal sealed class ProvisionClientEnrollmentScenario
    : IParameterizedScenario
{
    public string Name =>
        "provision-client-enrollment";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ProvisionClientEnrollmentArguments parsedArguments =
            ParseArguments(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    internal static ProvisionClientEnrollmentArguments ParseArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 4)
        {
            throw new ArgumentException(
                "Client enrollment provisioning requires a public "
                + "certificate file, an enrollment output file, a client "
                + "principal identifier, and a trust-policy identifier.",
                nameof(arguments));
        }

        return new ProvisionClientEnrollmentArguments(
            arguments[0],
            arguments[1],
            arguments[2],
            arguments[3]);
    }

    private static async Task ExecuteAsync(
        ProvisionClientEnrollmentArguments arguments)
    {
        using X509Certificate2 publicClientCertificate =
            X509CertificateLoader.LoadCertificateFromFile(
                arguments.PublicCertificateFilePath);

        if (publicClientCertificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "Client enrollment provisioning requires a public "
                + "certificate without a private key.");
        }

        await RuntimeHostClientCredentialEnrollmentProvisioner.CreateNewAsync(
            arguments.EnrollmentFilePath,
            publicClientCertificate,
            new RuntimeHostClientPrincipalId(
                arguments.PrincipalId),
            arguments.TrustPolicyId);

        Console.WriteLine(
            "Client enrollment provisioning");
        Console.WriteLine(
            "==============================");
        Console.WriteLine();
        Console.WriteLine(
            "Public client certificate loaded from external storage.");
        Console.WriteLine(
            "Client enrollment document created.");
        Console.WriteLine(
            "No private-key material was read or written.");
    }
}
