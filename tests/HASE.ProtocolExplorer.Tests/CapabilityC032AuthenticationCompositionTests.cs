using Hase.ProtocolExplorer.Scenarios;
using Hase.Runtime.Remote.Grpc.Adapter;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC032AuthenticationCompositionTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            13,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Create_EnrolledClientCertificate_ShouldAuthenticateClient01()
    {
        using CapabilityC032AuthenticationComposition composition =
            CapabilityC032AuthenticationComposition.Create(
                ValidationTimeUtc);

        RuntimeHostCertificateAuthenticationResult result =
            composition.AuthenticationService.Authenticate(
                composition.Certificates.ClientCertificate,
                ValidationTimeUtc);

        Assert.True(
            result.IsAuthenticated);
        Assert.NotNull(
            result.Principal);
        Assert.Equal(
            "client-01",
            result.Principal.PrincipalId);
        Assert.Equal(
            ValidationTimeUtc,
            result.Principal.AuthenticatedAtUtc);
        Assert.Equal(
            "c032-physical-validation-v1",
            result.Principal.TrustPolicyId);
    }

    [Fact]
    public void Create_DifferentClientCertificate_ShouldRejectAsUntrusted()
    {
        using CapabilityC032AuthenticationComposition composition =
            CapabilityC032AuthenticationComposition.Create(
                ValidationTimeUtc);
        using CapabilityC032CertificateSet otherCertificates =
            CapabilityC032CertificateSet.Create(
                ValidationTimeUtc);

        RuntimeHostCertificateAuthenticationResult result =
            composition.AuthenticationService.Authenticate(
                otherCertificates.ClientCertificate,
                ValidationTimeUtc);

        Assert.False(
            result.IsAuthenticated);
        Assert.Null(
            result.Principal);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateUntrusted,
            result.FailureReason);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.ChainNotTrusted,
            result.TrustFailureReason);
    }

    [Fact]
    public void Create_NonUtcValidationTime_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC032AuthenticationComposition.Create(
                ValidationTimeUtc.ToOffset(
                    TimeSpan.FromHours(
                        2))));
    }
}
