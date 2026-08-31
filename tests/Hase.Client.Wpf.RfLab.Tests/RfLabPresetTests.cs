using Hase.Client.Wpf.RfLab.Presets;

namespace Hase.Client.Wpf.RfLab.Tests;

/// <summary>
/// Presets are the operator's own files, written by the original
/// application over years. Reading them must be forgiving of what they
/// contain and strict about what it claims to have found.
/// </summary>
public sealed class RfLabPresetTests
{
    [Fact]
    public void APreset_ShouldReadTheValuesTheOriginalWrote()
    {
        RfLabPreset preset = RfLabPreset.FromLines("10MHz -20dB", OriginalFile());

        Assert.Equal("10MHz -20dB", preset.Name);
        Assert.Equal("Ffix 10MHz -10dB", preset.Description);
        Assert.Equal(0, preset.Mode);
        Assert.Equal(100_000, preset.Frequency);
        Assert.Equal(28, preset.Amplitude);
        Assert.Equal(800, preset.ModulationFrequency);
        Assert.Equal(60, preset.AmplitudeModulationDepth);
        Assert.Equal(50_000, preset.FrequencyDeviation);
        Assert.Equal("Bidirectional", preset.SweepMode);
        Assert.Equal("AD_8", preset.Sensor);
        Assert.Equal(10_000_000, preset.SweepStartFrequency);
        Assert.Equal(30_000_000, preset.SweepStopFrequency);
        Assert.Equal(10_000, preset.SweepTime);
        Assert.Equal(500, preset.MeasurementInterval);
        Assert.Equal(500, preset.MeasurementCount);
        Assert.Equal(250_000, preset.ClockFrequency0);
        Assert.Equal(2_000_000, preset.ClockFrequency1);
        Assert.Equal(3_000_000, preset.ClockFrequency2);
    }

    [Fact]
    public void AMissingValue_ShouldReadAsAbsentRatherThanZero()
    {
        // Absent and zero are different instructions: one leaves the panel
        // alone, the other commands the instrument to zero.
        RfLabPreset preset = RfLabPreset.FromLines("sparse", ["Name,sparse"]);

        Assert.Null(preset.Frequency);
        Assert.Null(preset.Amplitude);
        Assert.Null(preset.SweepMode);
        Assert.Null(preset.Sensor);
    }

    [Fact]
    public void AMalformedLine_ShouldNotCostTheWholePreset()
    {
        RfLabPreset preset = RfLabPreset.FromLines(
            "damaged",
            [
                "Frequency,21400000",
                "this line has no separator",
                ",value without a name",
                string.Empty,
                "Amplitude,not a number",
                "Tsweep,2000"
            ]);

        Assert.Equal(21_400_000, preset.Frequency);
        Assert.Equal(2_000, preset.SweepTime);

        // Present but unreadable is absent, not a guess.
        Assert.Null(preset.Amplitude);
    }

    [Fact]
    public void AValueContainingACommaShouldKeepEverythingAfterTheFirstOne()
    {
        RfLabPreset preset = RfLabPreset.FromLines(
            "described",
            ["Description,10 MHz, low level, for the bench"]);

        Assert.Equal("10 MHz, low level, for the bench", preset.Description);
    }

    private static string[] OriginalFile() =>
        [
            "Name,100KHz-28dB",
            "Description,Ffix 10MHz -10dB",
            "Mode,0",
            "Frequency,100000",
            "Amplitude,28",
            "FMod,800",
            "AMDepth,60",
            "Fdev,50000",
            "SweepMode,Bidirectional",
            "Sensor,AD_8",
            "Fstart,10000000",
            "Fstop,30000000",
            "Tsweep,10000",
            "Tmeasure,500",
            "Nmeasure,500",
            "MGENMode,0",
            "Baudrate,50",
            "Pattern,Message",
            "FCenter,1000000",
            "FShift,425",
            "PatternAmplitude,10",
            "Repeat,1",
            "SI5351Fclk0,250000",
            "SI5351Fclk1,2000000",
            "SI5351Fclk2,3000000"
        ];
}
