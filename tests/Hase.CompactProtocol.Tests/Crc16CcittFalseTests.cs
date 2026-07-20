using System.Text;
using Hase.CompactProtocol;

namespace Hase.CompactProtocol.Tests;

public sealed class Crc16CcittFalseTests
{
    [Fact]
    public void Calculate_StandardCheckValue_ShouldReturn29B1()
    {
        byte[] data =
            Encoding.ASCII.GetBytes(
                "123456789");

        ushort crc =
            Crc16CcittFalse.Calculate(
                data);

        Assert.Equal(
            0x29B1,
            crc);
    }

    [Fact]
    public void Calculate_EmptyData_ShouldReturnInitialValue()
    {
        ushort crc =
            Crc16CcittFalse.Calculate(
                []);

        Assert.Equal(
            0xFFFF,
            crc);
    }
}