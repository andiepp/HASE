using System.Reflection;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointOperationalCommandPortCompositionTests
{
    [Fact]
    public void OperationalResources_ExposeAttachmentBoundCommandPort()
    {
        PropertyInfo? property =
            typeof(IEndpointOperationalResources)
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

    [Fact]
    public void NativeResources_InheritSafeCommandPort()
    {
        Assert.True(
            typeof(IEndpointOperationalResources)
                .IsAssignableFrom(
                    typeof(NativeEndpointOperationalResources)));
    }

    [Fact]
    public void CompactResources_InheritSafeCommandPort()
    {
        Assert.True(
            typeof(IEndpointOperationalResources)
                .IsAssignableFrom(
                    typeof(CompactEndpointOperationalResources)));
    }
}