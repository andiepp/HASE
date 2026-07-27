namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Composes certificate authentication from externally provisioned client
/// enrollment configuration and an explicit certificate-trust policy.
/// </summary>
public static class RuntimeHostProvisionedCertificateAuthenticationFactory
{
    /// <summary>
    /// Creates certificate authentication backed by the operating system's
    /// X.509 trust configuration.
    /// </summary>
    public static Task<IRuntimeHostCertificateAuthenticationService>
        CreateSystemTrustAsync(
            string enrollmentFilePath,
            CancellationToken cancellationToken = default)
    {
        return CreateAsync(
            enrollmentFilePath,
            new RuntimeHostSystemCertificateTrustEvaluator(),
            cancellationToken);
    }

    /// <summary>
    /// Creates certificate authentication backed by the supplied explicit
    /// trust evaluator.
    /// </summary>
    public static async Task<IRuntimeHostCertificateAuthenticationService>
        CreateAsync(
            string enrollmentFilePath,
            IRuntimeHostCertificateTrustEvaluator trustEvaluator,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            trustEvaluator);

        RuntimeHostClientCredentialEnrollmentRegistry enrollmentRegistry =
            await RuntimeHostClientCredentialEnrollmentRegistryFile
                .LoadAsync(
                    enrollmentFilePath,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        return new RuntimeHostCertificateAuthenticationService(
            new RuntimeHostClientCertificateValidator(),
            new RuntimeHostCertificateTrustValidator(
                trustEvaluator),
            new RuntimeHostX509ClientCredentialIdentityExtractor(),
            new RuntimeHostClientAuthenticationService(
                enrollmentRegistry));
    }
}
