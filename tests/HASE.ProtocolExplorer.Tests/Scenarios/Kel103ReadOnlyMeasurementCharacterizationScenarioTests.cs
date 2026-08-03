using Hase.ProtocolExplorer.Scenarios;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.Scenarios;

public sealed class Kel103ReadOnlyMeasurementCharacterizationScenarioTests
{
    [Theory]
    [InlineData("voltage", 0)]
    [InlineData("CURRENT", 1)]
    [InlineData("Power", 2)]
    public void ParseCandidate_AcceptsOnlyCompiledInSelectors(
        string value,
        int expectedValue)
    {
        Assert.Equal(
            (Kel103MeasurementCandidate)expectedValue,
            Kel103ReadOnlyMeasurementCharacterizationScenario.ParseCandidate(value));
    }

    [Theory]
    [InlineData("input")]
    [InlineData("function")]
    [InlineData("arbitrary-query")]
    public void ParseCandidate_RejectsOtherSelectors(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyMeasurementCharacterizationScenario.ParseCandidate(value));
    }

    [Theory]
    [InlineData("cr")]
    [InlineData("CR")]
    public void ValidateTerminator_AcceptsOnlyCharacterizedCr(string value)
    {
        Kel103ReadOnlyMeasurementCharacterizationScenario.ValidateTerminator(value);
    }

    [Theory]
    [InlineData("lf")]
    [InlineData("crlf")]
    [InlineData("")]
    public void ValidateTerminator_RejectsOtherValues(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyMeasurementCharacterizationScenario.ValidateTerminator(value));
    }

    [Fact]
    public void ParseBaudRate_AcceptsOnlyCharacterizedRate()
    {
        Assert.Equal(
            115200,
            Kel103ReadOnlyMeasurementCharacterizationScenario.ParseBaudRate("115200"));
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyMeasurementCharacterizationScenario.ParseBaudRate("9600"));
    }

    [Fact]
    public void WriteHeader_DoesNotExposeSerialTargetOrMeasurementValue()
    {
        TextWriter original = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);
            Kel103ReadOnlyMeasurementCharacterizationScenario.WriteHeader(
                new SerialTransportOptions("PRIVATE-TARGET", 115200),
                Kel103MeasurementCandidate.Voltage);
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("External runtime argument", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-TARGET", output.ToString(), StringComparison.Ordinal);
    }
}
