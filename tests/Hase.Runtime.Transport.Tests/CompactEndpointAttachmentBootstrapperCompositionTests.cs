using Hase.CompactProtocol;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointAttachmentBootstrapperCompositionTests
{
    [Fact]
    public void Constructor_NullSerialByteStreamFactory_ShouldThrow()
    {
        // Arrange
        var repository =
            new CountingCompactEndpointDefinitionRepository();

        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentBootstrapper(
                null!,
                repository);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullCompactDefinitionRepository_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentBootstrapper(
                new CountingSerialByteStreamFactory(),
                (ICompactEndpointDefinitionRepository)null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_ValidDependencies_ShouldRemainPassive()
    {
        // Arrange
        var serialByteStreamFactory =
            new CountingSerialByteStreamFactory();

        var repository =
            new CountingCompactEndpointDefinitionRepository();

        // Act
        _ = new CompactEndpointAttachmentBootstrapper(
            serialByteStreamFactory,
            repository);

        // Assert
        Assert.Equal(
            0,
            serialByteStreamFactory.OpenCallCount);

        Assert.Equal(
            0,
            repository.FindCallCount);
    }

    private sealed class CountingSerialByteStreamFactory
        : ISerialByteStreamFactory
    {
        public int OpenCallCount
        {
            get;
            private set;
        }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            OpenCallCount++;

            throw new InvalidOperationException(
                "A serial byte stream was not expected.");
        }
    }

    private sealed class CountingCompactEndpointDefinitionRepository
        : ICompactEndpointDefinitionRepository
    {
        public int FindCallCount
        {
            get;
            private set;
        }

        public ValueTask<CompactEndpointDefinition?> FindAsync(
            Hase.Core.Domain.Descriptors.DescriptorReference reference,
            CancellationToken cancellationToken = default)
        {
            FindCallCount++;

            throw new InvalidOperationException(
                "A compact endpoint-definition lookup was not expected.");
        }
    }
}