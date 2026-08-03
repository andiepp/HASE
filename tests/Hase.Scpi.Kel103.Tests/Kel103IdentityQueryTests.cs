using System.Globalization;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103IdentityQueryTests
{
    [Fact]
    public void CommandText_IsFixedReadOnlyIdentificationQuery()
    {
        Assert.Equal("*IDN?", Kel103IdentityQuery.CommandText);
    }

    [Fact]
    public void ParseResponse_AcceptsCharacterizedIdentityAndOmitsSerialIdentity()
    {
        const string response = "RND 320-KEL103 V3.30 SN:REDACTED";

        Kel103Identity identity = Kel103IdentityQuery.ParseResponse(response);

        Assert.Equal("KEL-103", identity.ProductIdentity);
        Assert.Equal("V3.30", identity.FirmwareVersion);
        Assert.DoesNotContain("REDACTED", identity.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ParseResponse_IsInvariantUnderNonEnglishCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            Kel103Identity identity = Kel103IdentityQuery.ParseResponse(
                "RND 320-KEL103 V3.30 SN:REDACTED");

            Assert.Equal("V3.30", identity.FirmwareVersion);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("OTHER 320-KEL103 V3.30 SN:REDACTED")]
    [InlineData("RND OTHER-KEL103 V3.30 SN:REDACTED")]
    [InlineData("RND PREFIX-320-KEL103-SUFFIX V3.30 SN:REDACTED")]
    [InlineData("RND 320-KEL103 3.30 SN:REDACTED")]
    [InlineData("RND 320-KEL103 V3 SN:REDACTED")]
    [InlineData("RND 320-KEL103 V3.30.1 SN:REDACTED")]
    [InlineData("RND 320-KEL103 V3.30")]
    [InlineData("RND 320-KEL103 V3.30 SN:")]
    [InlineData("RND 320-KEL103 V3.30 SN:REDACTED EXTRA")]
    [InlineData("RND 320-KEL103 V3.30 SN:REDACTED\n")]
    [InlineData("RND 320-KEL103 V3.30 SN:REDACTED\rSECOND")]
    public void ParseResponse_RejectsUnsupportedOrMalformedIdentity(string response)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Kel103IdentityQuery.ParseResponse(response));

        Assert.DoesNotContain(response, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("REDACTED", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseResponse_RejectsMissingResponse(string? response)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Kel103IdentityQuery.ParseResponse(response!));
    }

    [Fact]
    public void ParseResponse_RejectsOversizedResponseWithoutEchoingIt()
    {
        string response = new('X', 512);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Kel103IdentityQuery.ParseResponse(response));

        Assert.DoesNotContain(response, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Identity_RejectsMissingPublishedValues()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Kel103Identity(null!, "V3.30"));
        Assert.ThrowsAny<ArgumentException>(() => new Kel103Identity("KEL-103", " "));
    }
}
