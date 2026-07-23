using System.Text;
using System.Text.Json;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostIdentityDocumentCodecTests
{
    [Fact]
    public void Serialize_ProducesCanonicalVersionOneDocument()
    {
        var runtimeHostId =
            new RuntimeHostId(
                "runtime-host-01234567-89ab-cdef-0123-456789abcdef");

        byte[] document =
            RuntimeHostIdentityDocumentCodec.Serialize(
                runtimeHostId);

        Assert.NotEmpty(
            document);

        Assert.NotEqual(
            0xEF,
            document[0]);

        Assert.Equal(
            (byte)'\n',
            document[^1]);

        using JsonDocument jsonDocument =
            JsonDocument.Parse(
                document);

        JsonElement root =
            jsonDocument.RootElement;

        Assert.Equal(
            2,
            root.EnumerateObject()
                .Count());

        Assert.Equal(
            1,
            root.GetProperty(
                    "formatVersion")
                .GetInt32());

        Assert.Equal(
            runtimeHostId.Value,
            root.GetProperty(
                    "runtimeHostId")
                .GetString());
    }

    [Fact]
    public void Serialize_NullIdentity_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostIdentityDocumentCodec.Serialize(
                null!));
    }

    [Fact]
    public void Parse_SerializedDocument_RoundTripsIdentity()
    {
        var runtimeHostId =
            new RuntimeHostId(
                "runtime-host-round-trip");

        byte[] document =
            RuntimeHostIdentityDocumentCodec.Serialize(
                runtimeHostId);

        RuntimeHostId parsedRuntimeHostId =
            RuntimeHostIdentityDocumentCodec.Parse(
                document);

        Assert.Equal(
            runtimeHostId,
            parsedRuntimeHostId);
    }

    [Fact]
    public void Parse_JsonFormattingWhitespace_IsAccepted()
    {
        byte[] document =
            Encoding.UTF8.GetBytes(
                """

                  {
                    "runtimeHostId": "runtime-host-whitespace",
                    "formatVersion": 1
                  }

                """);

        RuntimeHostId runtimeHostId =
            RuntimeHostIdentityDocumentCodec.Parse(
                document);

        Assert.Equal(
            "runtime-host-whitespace",
            runtimeHostId.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"formatVersion\":1}")]
    [InlineData("{\"runtimeHostId\":\"runtime-host-one\"}")]
    [InlineData("{\"formatVersion\":2,\"runtimeHostId\":\"runtime-host-one\"}")]
    [InlineData("{\"formatVersion\":\"1\",\"runtimeHostId\":\"runtime-host-one\"}")]
    [InlineData("{\"formatVersion\":1,\"runtimeHostId\":null}")]
    [InlineData("{\"formatVersion\":1,\"runtimeHostId\":\" \"}")]
    [InlineData("{\"formatVersion\":1,\"runtimeHostId\":\"runtime-host-one\",\"unknown\":true}")]
    [InlineData("{\"formatVersion\":1,\"formatVersion\":1,\"runtimeHostId\":\"runtime-host-one\"}")]
    [InlineData("{\"formatVersion\":1,\"runtimeHostId\":\"runtime-host-one\",\"runtimeHostId\":\"runtime-host-two\"}")]
    [InlineData("{\"formatVersion\":1,\"runtimeHostId\":\"runtime-host-one\",}")]
    [InlineData("{\"formatVersion\":1,\"runtimeHostId\":\"runtime-host-one\"}{}")]
    public void Parse_InvalidDocument_Throws(
        string text)
    {
        byte[] document =
            Encoding.UTF8.GetBytes(
                text);

        Assert.Throws<InvalidDataException>(
            () => RuntimeHostIdentityDocumentCodec.Parse(
                document));
    }

    [Fact]
    public void Parse_InvalidUtf8_Throws()
    {
        byte[] document =
        [
            0x7B,
            0x22,
            0xFF,
            0x22,
            0x3A,
            0x31,
            0x7D,
        ];

        Assert.Throws<InvalidDataException>(
            () => RuntimeHostIdentityDocumentCodec.Parse(
                document));
    }

    [Fact]
    public void Parse_OversizedDocument_Throws()
    {
        byte[] document =
            new byte[
                RuntimeHostIdentityDocumentCodec.MaximumDocumentByteCount
                + 1];

        Assert.Throws<InvalidDataException>(
            () => RuntimeHostIdentityDocumentCodec.Parse(
                document));
    }
}