using HASE.ProtocolExplorer.Deployment;
using Xunit;

namespace HASE.ProtocolExplorer.Tests.Deployment;

public sealed class Esp32PhysicalDeploymentPreflightAssessmentTests
{
    [Fact]
    public void AllEvidenceReady_ShouldBeReady()
    {
        Esp32PhysicalDeploymentPreflightAssessment assessment = Create();

        Assert.True(assessment.IsReady);
        Assert.All(assessment.Readiness, result => Assert.True(result.Ready));
        Assert.False(assessment.RequiresRecovery);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void AnyBlockedEvidence_ShouldFailClosedWithoutRecovery(int blockedIndex)
    {
        bool[] results = [true, true, true, true, true, true, true, true, true];
        results[blockedIndex] = false;
        var assessment = new Esp32PhysicalDeploymentPreflightAssessment(
            results[0], results[1], results[2], results[3], results[4],
            results[5], results[6], results[7], results[8]);

        Assert.False(assessment.IsReady);
        Assert.False(assessment.Readiness[blockedIndex].Ready);
        Assert.False(assessment.RequiresRecovery);
    }

    [Fact]
    public void RejectedPreflight_ShouldRequireNoRecovery()
    {
        var assessment = new Esp32PhysicalDeploymentPreflightAssessment(
            true, true, true, true, true, true, true, false, true);

        Assert.False(assessment.IsReady);
        Assert.False(assessment.RequiresRecovery);
    }

    private static Esp32PhysicalDeploymentPreflightAssessment Create() =>
        new(true, true, true, true, true, true, true, true, true);
}
