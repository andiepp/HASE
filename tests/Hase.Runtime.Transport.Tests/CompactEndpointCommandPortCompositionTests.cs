using System.Reflection;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointCommandPortCompositionTests
{
    [Fact]
    public void CompactResources_RetainAttachmentBoundCommandPort()
    {
        PropertyInfo? property =
            typeof(CompactEndpointOperationalResources)
                .GetProperty(
                    "CommandOperations",
                    BindingFlags.Instance
                    | BindingFlags.Public);

        Assert.NotNull(
            property);

        Assert.Equal(
            typeof(IEndpointAttachmentCommandOperations),
            property.PropertyType);
    }
}