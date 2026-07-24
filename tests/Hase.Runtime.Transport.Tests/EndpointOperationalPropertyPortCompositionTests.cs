using System.Reflection;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointOperationalPropertyPortCompositionTests
{
    [Fact]
    public void NativeResources_RetainAttachmentBoundPropertyPort()
    {
        PropertyInfo? property =
            typeof(NativeEndpointOperationalResources)
                .GetProperty(
                    "PropertyOperations",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic);

        Assert.NotNull(
            property);

        Assert.Equal(
            typeof(IEndpointAttachmentPropertyOperations),
            property.PropertyType);
    }

    [Fact]
    public void CompactResources_RetainAttachmentBoundPropertyPort()
    {
        PropertyInfo? property =
            typeof(CompactEndpointOperationalResources)
                .GetProperty(
                    "PropertyOperations",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic);

        Assert.NotNull(
            property);

        Assert.Equal(
            typeof(IEndpointAttachmentPropertyOperations),
            property.PropertyType);
    }
}