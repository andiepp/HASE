using Hase.Core.Domain.Data;

namespace Hase.Core.Tests;

public sealed class ByteArrayDataDescriptorTests
{
    [Fact]
    public void Constructor_CreatesDataDescriptor()
    {
        ByteArrayDataDescriptor descriptor = new();

        Assert.IsAssignableFrom<DataDescriptor>(
            descriptor);
    }

    [Fact]
    public void Equality_TwoDescriptorsAreEqual()
    {
        Assert.Equal(
            new ByteArrayDataDescriptor(),
            new ByteArrayDataDescriptor());
    }
}
