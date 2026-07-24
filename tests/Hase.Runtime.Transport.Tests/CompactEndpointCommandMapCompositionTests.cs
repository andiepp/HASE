using System.Reflection;
using Hase.CompactProtocol;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointCommandMapCompositionTests
{
    [Fact]
    public void CompactResources_RetainValidatedCommandMap()
    {
        PropertyInfo? property =
            typeof(CompactEndpointOperationalResources)
                .GetProperty(
                    "CommandMap",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic);

        Assert.NotNull(
            property);

        Assert.Equal(
            typeof(CompactCommandMap),
            property.PropertyType);
    }
}