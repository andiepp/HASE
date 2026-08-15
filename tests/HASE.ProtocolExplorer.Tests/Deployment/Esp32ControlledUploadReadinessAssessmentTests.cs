using HASE.ProtocolExplorer.Deployment;
using Xunit;

namespace HASE.ProtocolExplorer.Tests.Deployment;

public sealed class Esp32ControlledUploadReadinessAssessmentTests
{
    [Fact]
    public void AllEvidenceReady_ShouldBeReadyWithoutRecovery()
    {
        Esp32ControlledUploadReadinessAssessment assessment = Create();

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
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void AnyBlockedEvidence_ShouldFailClosedWithoutRecovery(int blockedIndex)
    {
        bool[] results = Enumerable.Repeat(true, 13).ToArray();
        results[blockedIndex] = false;
        var assessment = new Esp32ControlledUploadReadinessAssessment(
            results[0], results[1], results[2], results[3],
            results[4], results[5], results[6], results[7],
            results[8], results[9], results[10], results[11], results[12]);

        Assert.False(assessment.IsReady);
        Assert.False(assessment.Readiness[blockedIndex].Ready);
        Assert.False(assessment.RequiresRecovery);
    }

    [Fact]
    public void RejectedReadiness_ShouldRequireNoPhysicalRecovery()
    {
        var assessment = new Esp32ControlledUploadReadinessAssessment(
            true, true, true, true, true, true, true,
            true, false, false, true, true, true);

        Assert.False(assessment.IsReady);
        Assert.False(assessment.RequiresRecovery);
    }

    private static Esp32ControlledUploadReadinessAssessment Create() =>
        new(true, true, true, true, true, true, true,
            true, true, true, true, true, true);
}
