using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class MiniPcRuntimeHostInstallationAssessmentTests
{
    [Fact]
    public void AllResultsReady_ShouldBeReady()
    {
        MiniPcRuntimeHostInstallationAssessment assessment = Create();

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
        var assessment = new MiniPcRuntimeHostInstallationAssessment(
            results[0], results[1], results[2], results[3], results[4], results[5]);

        Assert.False(assessment.IsReady);
        Assert.False(assessment.Readiness[blockedIndex].Ready);
    }

    private static MiniPcRuntimeHostInstallationAssessment Create() =>
        new(true, true, true, true, true, true);
}
