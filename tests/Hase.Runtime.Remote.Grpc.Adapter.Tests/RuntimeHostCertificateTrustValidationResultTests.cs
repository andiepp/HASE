namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCertificateTrustValidationResultTests
{
    [Fact]
    public void Trusted_ShouldCreateSuccessfulResult()
    {
        RuntimeHostCertificateTrustValidationResult result =
            RuntimeHostCertificateTrustValidationResult.Trusted();

        Assert.True(
            result.IsTrusted);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.None,
            result.FailureReason);
    }

    [Fact]
    public void Untrusted_ShouldPreserveFailureReason()
    {
        RuntimeHostCertificateTrustValidationResult result =
            RuntimeHostCertificateTrustValidationResult.Untrusted(
                RuntimeHostCertificateTrustFailureReason.ChainNotTrusted);

        Assert.False(
            result.IsTrusted);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.ChainNotTrusted,
            result.FailureReason);
    }

    [Fact]
    public void Untrusted_NoneReason_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "failureReason",
            () =>
                RuntimeHostCertificateTrustValidationResult.Untrusted(
                    RuntimeHostCertificateTrustFailureReason.None));
    }

    [Fact]
    public void Untrusted_UnknownReason_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "failureReason",
            () =>
                RuntimeHostCertificateTrustValidationResult.Untrusted(
                    (RuntimeHostCertificateTrustFailureReason)999));
    }
}
