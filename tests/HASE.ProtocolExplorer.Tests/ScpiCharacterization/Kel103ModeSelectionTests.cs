using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103ModeSelectionTests
{
    [Theory]
    [InlineData(0, "cc", ":FUNCtion CC", "CC")]
    [InlineData(1, "cv", ":FUNCtion CV", "CV")]
    [InlineData(2, "cr", ":FUNCtion CR", "CR")]
    [InlineData(3, "cw", ":FUNCtion CW", "CW")]
    [InlineData(4, "short", ":FUNCtion SHORt", "SHORt")]
    public void Mapping_IsExact(
        int selectionValue,
        string argument,
        string command,
        string readback)
    {
        var selection = (Kel103ModeSelection)selectionValue;

        Assert.Equal(argument, selection.ToArgumentValue());
        Assert.Equal(command, selection.ToCommandText());
        Assert.Equal(readback, selection.ToReadbackToken());
    }

    [Fact]
    public void Mapping_RejectsUndefinedSelection()
    {
        var selection = (Kel103ModeSelection)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => selection.ToArgumentValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.ToCommandText());
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.ToReadbackToken());
    }
}
