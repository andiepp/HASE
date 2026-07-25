using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Evaluates certificate trust through the .NET and operating-system
/// X.509 chain implementation.
/// </summary>
public sealed class RuntimeHostSystemCertificateTrustEvaluator
    : IRuntimeHostCertificateTrustEvaluator
{
    /// <inheritdoc />
    public bool IsTrusted(
        X509Certificate2 certificate,
        DateTimeOffset validationTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(
            certificate);

        if (validationTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The certificate-trust validation time must use UTC.",
                nameof(validationTimeUtc));
        }

        using X509Chain chain =
            new();

        chain.ChainPolicy.TrustMode =
            X509ChainTrustMode.System;
        chain.ChainPolicy.RevocationMode =
            X509RevocationMode.NoCheck;
        chain.ChainPolicy.RevocationFlag =
            X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags =
            X509VerificationFlags.NoFlag;
        chain.ChainPolicy.VerificationTime =
            validationTimeUtc.UtcDateTime;
        chain.ChainPolicy.DisableCertificateDownloads =
            true;

        return chain.Build(
            certificate);
    }
}
