using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class MiniPcInboundClientTrustAssessmentTests
{
    [Fact]
    public void IsReady_WhenAllInputsAreReady_ShouldBeTrue()
    {
        Assert.True(Create().IsReady);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void IsReady_WhenOneInputIsNotReady_ShouldBeFalse(int failedInput)
    {
        bool[] values = [true, true, true, true, true, true];
        values[failedInput] = false;

        var assessment = new MiniPcInboundClientTrustAssessment(
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[5]);

        Assert.False(assessment.IsReady);
    }

    private static MiniPcInboundClientTrustAssessment Create() =>
        new(
            ClientPrivateKeyPreserved: true,
            TransferCertificateIsPublicOnly: true,
            CredentialMatchesEnrollment: true,
            TrustedClientCertificateReady: true,
            RuntimeHostConfigurationPreserved: true,
            RuntimeHostIdentityPreserved: true);
}
