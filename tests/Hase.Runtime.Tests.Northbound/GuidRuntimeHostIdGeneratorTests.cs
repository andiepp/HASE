using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class GuidRuntimeHostIdGeneratorTests
{
    private const string RuntimeHostIdPrefix =
        "runtime-host-";

    [Fact]
    public void Generate_ReturnsPrefixedLowercaseCanonicalGuid()
    {
        var generator =
            new GuidRuntimeHostIdGenerator();

        RuntimeHostId runtimeHostId =
            generator.Generate();

        Assert.StartsWith(
            RuntimeHostIdPrefix,
            runtimeHostId.Value);

        string guidText =
            runtimeHostId.Value[
                RuntimeHostIdPrefix.Length..];

        Assert.True(
            Guid.TryParseExact(
                guidText,
                "D",
                out _));

        Assert.Equal(
            guidText.ToLowerInvariant(),
            guidText);
    }

    [Fact]
    public void Generate_RepeatedCalls_ReturnDistinctIdentities()
    {
        var generator =
            new GuidRuntimeHostIdGenerator();

        RuntimeHostId first =
            generator.Generate();

        RuntimeHostId second =
            generator.Generate();

        Assert.NotEqual(
            first,
            second);
    }
}