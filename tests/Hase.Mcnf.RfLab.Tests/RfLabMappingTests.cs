namespace Hase.Mcnf.RfLab.Tests;

public sealed class RfLabMappingTests
{
    [Fact]
    public void TargetMappings_CarryUniqueIdentifiersAndPaths()
    {
        Assert.Equal(
            RfLabTargetMapping.All.Count,
            RfLabTargetMapping.All.Select(mapping => mapping.PropertyId).Distinct().Count());
        Assert.Equal(
            RfLabTargetMapping.All.Count,
            RfLabTargetMapping.All.Select(mapping => mapping.PropertyPath).Distinct().Count());
    }

    [Fact]
    public void TargetMappings_DefaultsLieWithinTheCharacterizedRanges()
    {
        Assert.All(RfLabTargetMapping.All, mapping =>
        {
            Assert.InRange(mapping.DefaultValue, mapping.Minimum, mapping.Maximum);
        });
    }

    [Fact]
    public void TargetMappings_CoverEachClockChannelExactlyOnce()
    {
        int[] clockChannels = RfLabTargetMapping.All
            .Where(mapping => mapping.ClockChannel is not null)
            .Select(mapping => mapping.ClockChannel!.Value)
            .Order()
            .ToArray();

        Assert.Equal(new[] { 0, 1, 2 }, clockChannels);
    }

    [Fact]
    public void CommandMappings_CarryUniquePaths()
    {
        Assert.Equal(
            RfLabCommandMapping.All.Count,
            RfLabCommandMapping.All.Select(mapping => mapping.CommandPath).Distinct().Count());
    }

    [Fact]
    public void CommandMappings_CarryTheMetadataTheirKindRequires()
    {
        Assert.All(RfLabCommandMapping.All, mapping =>
        {
            switch (mapping.Kind)
            {
                case RfLabCommandKind.StartSweep:
                    Assert.NotNull(mapping.SweepMode);
                    break;
                case RfLabCommandKind.ApplyClock:
                    Assert.NotNull(mapping.ClockChannel);
                    Assert.InRange(mapping.ClockChannel.Value, 0, 2);
                    break;
                case RfLabCommandKind.IndicatorControl:
                    Assert.NotNull(mapping.IndicatorEnable);
                    break;
                default:
                    Assert.Null(mapping.SweepMode);
                    Assert.Null(mapping.ClockChannel);
                    Assert.Null(mapping.IndicatorEnable);
                    break;
            }
        });
    }

    [Fact]
    public void CommandMappings_CoverEverySweepModeExactlyOnce()
    {
        RfLabSweepMode[] sweepModes = RfLabCommandMapping.All
            .Where(mapping => mapping.SweepMode is not null)
            .Select(mapping => mapping.SweepMode!.Value)
            .Order()
            .ToArray();

        Assert.Equal(
            new[]
            {
                RfLabSweepMode.Bidirectional,
                RfLabSweepMode.Ramp,
                RfLabSweepMode.SingleRamp
            },
            sweepModes);
    }
}
