using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointAttachmentHostCompactSerialTests
{
    [Fact]
    public async Task CreateCompactSerial_ShouldCreateOwnedContextAndInventory()
    {
        // Act
        await using RuntimeEndpointAttachmentHost host =
            RuntimeEndpointAttachmentHost.CreateCompactSerial(
                CreateRepository(),
                new DefaultRuntimeEndpointReconnectPolicy());

        // Assert
        Assert.NotNull(
            host.RuntimeContext);

        Assert.Empty(
            host.RuntimeContext.Endpoints);

        Assert.IsType<RuntimeEndpointAttachmentInventory>(
            host.AttachmentInventory);

        Assert.Empty(
            host.AttachmentInventory.List());
    }

    [Fact]
    public async Task CreateCompactSerial_Attachment_ShouldRouteThroughCompactService()
    {
        // Arrange
        await using RuntimeEndpointAttachmentHost host =
            RuntimeEndpointAttachmentHost.CreateCompactSerial(
                CreateRepository(),
                new DefaultRuntimeEndpointReconnectPolicy());

        var request =
            new EndpointAttachmentRequest(
                new TestEndpointConnectionDefinition(),
                new TestEndpointDescriptorSource());

        // Act
        Task Act()
        {
            return host.AttachmentInventory.AttachAsync(
                request);
        }

        // Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            Act);

        Assert.Empty(
            host.AttachmentInventory.List());

        Assert.Empty(
            host.RuntimeContext.Endpoints);
    }

    [Fact]
    public void CreateCompactSerial_NullDefinitionRepository_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = RuntimeEndpointAttachmentHost.CreateCompactSerial(
                null!,
                new DefaultRuntimeEndpointReconnectPolicy());
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void CreateCompactSerial_NullReconnectPolicy_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = RuntimeEndpointAttachmentHost.CreateCompactSerial(
                CreateRepository(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static ICompactEndpointDefinitionRepository
        CreateRepository()
    {
        return new InMemoryCompactEndpointDefinitionRepository(
            [
                new CompactEndpointDefinition(
                    new DescriptorReference(
                        new Hase.Core.Domain.Identity.DescriptorId(
                            "arduino-uno-validation"),
                        version: 1),
                    new EndpointDescriptorDefinition(),
                    [])
            ]);
    }

    private sealed class TestEndpointConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public Hase.Core.Domain.Identity.EndpointId? ExpectedEndpointId =>
            null;
    }

    private sealed class TestEndpointDescriptorSource
        : IEndpointDescriptorSource
    {
    }
}