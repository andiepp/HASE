namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientCertificateValidationResultTests
{
    [Fact]
    public void Valid_ShouldCreateSuccessfulResult()
    {
        RuntimeHostClientCertificateValidationResult result =
            RuntimeHostClientCertificateValidationResult.Valid();

        Assert.True(
            result.IsValid);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason.None,
            result.FailureReason);
    }

    [Fact]
    public void Invalid_ShouldPreserveFailureReason()
    {
        RuntimeHostClientCertificateValidationResult result =
            RuntimeHostClientCertificateValidationResult.Invalid(
                RuntimeHostClientCertificateValidationFailureReason
                    .CertificateExpired);

        Assert.False(
            result.IsValid);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason
                .CertificateExpired,
            result.FailureReason);
    }

    [Fact]
    public void Invalid_NoneReason_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "failureReason",
            () =>
                RuntimeHostClientCertificateValidationResult.Invalid(
                    RuntimeHostClientCertificateValidationFailureReason.None));
    }

    [Fact]
    public void Invalid_UnknownReason_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "failureReason",
            () =>
                RuntimeHostClientCertificateValidationResult.Invalid(
                    (RuntimeHostClientCertificateValidationFailureReason)999));
    }
}
