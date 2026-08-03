using Xunit;
using System.Text;
using Hase.ProtocolExplorer.ScpiCharacterization;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103CommandTerminatorTests
{
    [Theory]
    [InlineData(
        0,
        "*IDN?\r")]
    [InlineData(
        1,
        "*IDN?\n")]
    [InlineData(
        2,
        "*IDN?\r\n")]
    public void CreateRequest_AppendsExactlySelectedTerminator(
        int terminatorValue,
        string expected)
    {
        var terminator =
            (Kel103CommandTerminator)terminatorValue;

        byte[] request =
            Kel103ReadOnlySerialCharacterizer.CreateRequest(
                terminator);

        Assert.Equal(
            Encoding.ASCII.GetBytes(
                expected),
            request);
    }

    [Fact]
    public void ToBytes_RejectsUndefinedTerminator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ((Kel103CommandTerminator)999).ToBytes());
    }
}
