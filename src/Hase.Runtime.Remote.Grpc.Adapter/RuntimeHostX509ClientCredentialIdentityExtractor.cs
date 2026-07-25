using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies an already validated X.509 client certificate by the SHA-256
/// hash of its complete DER encoding.
/// </summary>
public sealed class RuntimeHostX509ClientCredentialIdentityExtractor
    : IRuntimeHostX509ClientCredentialIdentityExtractor
{
    private const string CredentialIdPrefix =
        "x509-sha256:";

    /// <inheritdoc />
    public RuntimeHostClientCredentialIdentity Extract(
        X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(
            certificate);

        byte[] certificateHash =
            certificate.GetCertHash(
                HashAlgorithmName.SHA256);

        if (certificateHash.Length == 0)
        {
            throw new InvalidOperationException(
                "The X.509 certificate did not produce a SHA-256 "
                + "credential identifier.");
        }

        string credentialId =
            CredentialIdPrefix
            + Convert.ToHexString(
                certificateHash)
                .ToLowerInvariant();

        return new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(
                credentialId));
    }
}
