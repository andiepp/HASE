namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientCertificateValidationFailureReasonTests
{
    [Fact]
    public void None_ShouldBeDefault()
    {
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason.None,
            default);
    }

    [Fact]
    public void DefinedFailures_ShouldRemainDistinct()
    {
        RuntimeHostClientCertificateValidationFailureReason[] failures =
        [
            RuntimeHostClientCertificateValidationFailureReason
                .CertificateMissing,
            RuntimeHostClientCertificateValidationFailureReason
                .CertificateNotYetValid,
            RuntimeHostClientCertificateValidationFailureReason
                .CertificateExpired,
            RuntimeHostClientCertificateValidationFailureReason
                .MissingClientAuthenticationUsage,
            RuntimeHostClientCertificateValidationFailureReason
                .MalformedCertificate
        ];

        Assert.Equal(
            failures.Length,
            failures.Distinct().Count());
    }
}
