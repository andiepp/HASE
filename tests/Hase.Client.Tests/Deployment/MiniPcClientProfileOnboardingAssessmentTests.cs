using Hase.Client.Deployment;

namespace Hase.Client.Tests.Deployment;

public sealed class MiniPcClientProfileOnboardingAssessmentTests
{
    [Fact]
    public void AllResultsReady_ShouldBeReady()
    {
        var assessment = new MiniPcClientProfileOnboardingAssessment(
            true, true, true, true, true, true);

        Assert.True(assessment.IsReady);
        Assert.All(assessment.Readiness, result => Assert.True(result.Ready));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void AnyBlockedResult_ShouldFailClosed(int blockedIndex)
    {
        bool[] results = [true, true, true, true, true, true];
        results[blockedIndex] = false;
        var assessment = new MiniPcClientProfileOnboardingAssessment(
            results[0], results[1], results[2], results[3], results[4], results[5]);

        Assert.False(assessment.IsReady);
        Assert.False(assessment.Readiness[blockedIndex].Ready);
    }
}
