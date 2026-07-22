using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointAttachmentBootstrapResultTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldExposeValues()
    {
        // Arrange
        EndpointId endpointId =
            CreateEndpointId();

        DescriptorReference descriptorReference =
            CreateDescriptorReference();

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        EndpointDescriptor descriptor =
            descriptorDefinition.Materialize(
                endpointId);

        // Act
        var result =
            new CompactEndpointAttachmentBootstrapResult(
                endpointId,
                descriptorReference,
                descriptorDefinition,
                descriptor);

        // Assert
        Assert.Same(
            endpointId,
            result.EndpointId);

        Assert.Same(
            descriptorReference,
            result.DescriptorReference);

        Assert.Same(
            descriptorDefinition,
            result.DescriptorDefinition);

        Assert.Same(
            descriptor,
            result.Descriptor);
    }

    [Fact]
    public void Constructor_NullEndpointId_ShouldThrow()
    {
        // Arrange
        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentBootstrapResult(
                null!,
                CreateDescriptorReference(),
                descriptorDefinition,
                descriptorDefinition.Materialize(
                    CreateEndpointId()));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptorReference_ShouldThrow()
    {
        // Arrange
        EndpointId endpointId =
            CreateEndpointId();

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentBootstrapResult(
                endpointId,
                null!,
                descriptorDefinition,
                descriptorDefinition.Materialize(
                    endpointId));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptorDefinition_ShouldThrow()
    {
        // Arrange
        EndpointId endpointId =
            CreateEndpointId();

        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentBootstrapResult(
                endpointId,
                CreateDescriptorReference(),
                null!,
                new EndpointDescriptor(
                    endpointId));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptor_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentBootstrapResult(
                CreateEndpointId(),
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_MismatchedDescriptorIdentity_ShouldThrow()
    {
        // Arrange
        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentBootstrapResult(
                CreateEndpointId(),
                CreateDescriptorReference(),
                descriptorDefinition,
                descriptorDefinition.Materialize(
                    new EndpointId(
                        "different-endpoint")));
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public async Task BootstrapperContract_ShouldReturnBootstrapResult()
    {
        // Arrange
        EndpointId endpointId =
            CreateEndpointId();

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        var expectedResult =
            new CompactEndpointAttachmentBootstrapResult(
                endpointId,
                CreateDescriptorReference(),
                descriptorDefinition,
                descriptorDefinition.Materialize(
                    endpointId));

        ICompactEndpointAttachmentBootstrapper bootstrapper =
            new StubCompactEndpointAttachmentBootstrapper(
                expectedResult);

        SerialEndpointConnectionDefinition connectionDefinition =
            SerialEndpointConnectionDefinition.FromConfiguration(
                new Hase.Transport.Serial.SerialTransportOptions(
                    "COM10",
                    115200));

        // Act
        CompactEndpointAttachmentBootstrapResult result =
            await bootstrapper.BootstrapAsync(
                connectionDefinition);

        // Assert
        Assert.Same(
            expectedResult,
            result);
    }

    private static EndpointId CreateEndpointId()
    {
        return new EndpointId(
            "arduino-uno-01");
    }

    private static DescriptorReference CreateDescriptorReference()
    {
        return new DescriptorReference(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);
    }

    private sealed class StubCompactEndpointAttachmentBootstrapper
        : ICompactEndpointAttachmentBootstrapper
    {
        private readonly CompactEndpointAttachmentBootstrapResult _result;

        public StubCompactEndpointAttachmentBootstrapper(
            CompactEndpointAttachmentBootstrapResult result)
        {
            _result =
                result;
        }

        public Task<CompactEndpointAttachmentBootstrapResult> BootstrapAsync(
            SerialEndpointConnectionDefinition connectionDefinition,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                connectionDefinition);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _result);
        }
    }
}