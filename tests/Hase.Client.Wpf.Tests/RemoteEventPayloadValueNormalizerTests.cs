using Hase.Client.Wpf.Services;
using Hase.Core.Domain.Data;

namespace Hase.Client.Wpf.Tests;

public sealed class RemoteEventPayloadValueNormalizerTests
{
    [Fact]
    public void Normalize_AbsentValue_ShouldReturnNull()
    {
        Assert.Null(
            RemoteEventPayloadValueNormalizer.Normalize(
                null));
    }

    [Fact]
    public void Normalize_Boolean_ShouldReturnBoolean()
    {
        Assert.True(
            Assert.IsType<bool>(
                RemoteEventPayloadValueNormalizer.Normalize(
                    RemoteValue.FromBoolean(
                        true))));
    }

    [Fact]
    public void Normalize_Numeric_ShouldReturnDouble()
    {
        Assert.Equal(
            12.5,
            Assert.IsType<double>(
                RemoteEventPayloadValueNormalizer.Normalize(
                    RemoteValue.FromNumeric(
                        12.5))));
    }

    [Fact]
    public void Normalize_String_ShouldReturnString()
    {
        Assert.Equal(
            "ready",
            Assert.IsType<string>(
                RemoteEventPayloadValueNormalizer.Normalize(
                    RemoteValue.FromString(
                        "ready"))));
    }

    [Fact]
    public void Normalize_ByteArray_ShouldPreserveValue()
    {
        ByteArrayValue value =
            new(
                new byte[]
                {
                    0x00,
                    0x53,
                    0xFF
                });

        Assert.Same(
            value,
            RemoteEventPayloadValueNormalizer.Normalize(
                RemoteValue.FromByteArray(
                    value)));
    }
}
