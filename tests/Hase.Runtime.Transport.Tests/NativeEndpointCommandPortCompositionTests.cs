using System.Reflection;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeEndpointCommandPortCompositionTests
{
    [Fact]
    public void NativeResources_RetainAttachmentBoundCommandPort()
    {
        PropertyInfo? property =
            typeof(NativeEndpointOperationalResources)
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