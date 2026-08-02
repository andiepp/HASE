using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class MiniPcClientSecurityProvisioningAssessmentTests
{
    [Fact]
    public void AllResultsReady_ShouldBeReady()
    {
        MiniPcClientSecurityProvisioningAssessment assessment = Create();

        Assert.True(assessment.IsReady);
        Assert.All(assessment.Readiness, result => Assert.True(result.Ready));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void AnyBlockedResult_ShouldFailClosed(int blockedIndex)
    {
        bool[] results = [true, true, true, true, true];
        results[blockedIndex] = false;

        var assessment = new MiniPcClientSecurityProvisioningAssessment(
            results[0], results[1], results[2], results[3], results[4]);

        Assert.False(assessment.IsReady);
        Assert.False(assessment.Readiness[blockedIndex].Ready);
    }

    private static MiniPcClientSecurityProvisioningAssessment Create() =>
        new(true, true, true, true, true);
}
