using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCertificateTrustValidatorTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            25,
            22,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Constructor_NullEvaluator_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "trustEvaluator",
            () =>
                new RuntimeHostCertificateTrustValidator(
                    null!));
    }

    [Fact]
    public void Validate_MissingCertificate_ShouldFailWithoutEvaluation()
    {
        TrackingTrustEvaluator evaluator =
            new(
                true);
        RuntimeHostCertificateTrustValidator validator =
            new(
                evaluator);

        RuntimeHostCertificateTrustValidationResult result =
            validator.Validate(
                null,
                ValidationTimeUtc);

        Assert.False(
            result.IsTrusted);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.CertificateMissing,
            result.FailureReason);
        Assert.False(
            evaluator.WasCalled);
    }

    [Fact]
    public void Validate_TrustedCertificate_ShouldSucceed()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostCertificateTrustValidator validator =
            new(
                new TrackingTrustEvaluator(
                    true));

        RuntimeHostCertificateTrustValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.True(
            result.IsTrusted);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.None,
            result.FailureReason);
    }

    [Fact]
    public void Validate_UntrustedCertificate_ShouldFailClosed()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostCertificateTrustValidator validator =
            new(
                new TrackingTrustEvaluator(
                    false));

        RuntimeHostCertificateTrustValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.False(
            result.IsTrusted);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.ChainNotTrusted,
            result.FailureReason);
    }

    [Fact]
    public void Validate_CryptographicFailure_ShouldReturnEvaluationFailure()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostCertificateTrustValidator validator =
            new(
                new ThrowingTrustEvaluator(
                    new CryptographicException(
                        "Trust evaluation failed.")));

        RuntimeHostCertificateTrustValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.False(
            result.IsTrusted);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.TrustEvaluationFailed,
            result.FailureReason);
    }

    [Fact]
    public void Validate_NonUtcTime_ShouldThrowBeforeEvaluation()
    {
        TrackingTrustEvaluator evaluator =
            new(
                true);
        RuntimeHostCertificateTrustValidator validator =
            new(
                evaluator);
        DateTimeOffset nonUtcTime =
            new(
                2026,
                7,
                26,
                0,
                30,
                0,
                TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            "validationTimeUtc",
            () =>
                validator.Validate(
                    null,
                    nonUtcTime));

        Assert.False(
            evaluator.WasCalled);
    }

    [Fact]
    public void CreateSystemTrust_ShouldCreateUsableValidator()
    {
        RuntimeHostCertificateTrustValidator validator =
            RuntimeHostCertificateTrustValidator.CreateSystemTrust();

        RuntimeHostCertificateTrustValidationResult result =
            validator.Validate(
                null,
                ValidationTimeUtc);

        Assert.False(
            result.IsTrusted);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.CertificateMissing,
            result.FailureReason);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using RSA key =
            RSA.Create(
                2048);

        CertificateRequest request =
            new(
                new X500DistinguishedName(
                    "CN=hase-client"),
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            ValidationTimeUtc.AddDays(-1),
            ValidationTimeUtc.AddDays(1));
    }

    private sealed class TrackingTrustEvaluator
        : IRuntimeHostCertificateTrustEvaluator
    {
        private readonly bool result;

        public TrackingTrustEvaluator(
            bool result)
        {
            this.result = result;
        }

        public bool WasCalled { get; private set; }

        public bool IsTrusted(
            X509Certificate2 certificate,
            DateTimeOffset validationTimeUtc)
        {
            WasCalled = true;
            return result;
        }
    }

    private sealed class ThrowingTrustEvaluator
        : IRuntimeHostCertificateTrustEvaluator
    {
        private readonly Exception exception;

        public ThrowingTrustEvaluator(
            Exception exception)
        {
            this.exception = exception;
        }

        public bool IsTrusted(
            X509Certificate2 certificate,
            DateTimeOffset validationTimeUtc)
        {
            throw exception;
        }
    }
}
