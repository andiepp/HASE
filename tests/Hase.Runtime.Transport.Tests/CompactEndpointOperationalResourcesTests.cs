using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointOperationalResourcesTests
{
    [Fact]
    public async Task CreateSerial_ShouldAssembleOperationalResourceGraph()
    {
        // Arrange
        CompactEndpointDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .CreateEndpoint(
                    definition.DescriptorDefinition.Materialize(
                        new EndpointId(
                            "arduino-uno-01")));

        var serialByteStreamFactory =
            new CountingSerialByteStreamFactory();

        var definitionRepository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    definition
                ]);

        // Act
        CompactEndpointOperationalResources resources =
            CompactEndpointOperationalResources.CreateSerial(
                CreateConnectionDefinition(),
                definition,
                runtimeEndpoint,
                serialByteStreamFactory,
                definitionRepository,
                new DefaultRuntimeEndpointReconnectPolicy(),
                new CompactEndpointHealthProbeOptions(
                    TimeSpan.FromSeconds(
                        1),
                    TimeSpan.FromSeconds(
                        3)));

        // Assert
        Assert.Same(
            definition,
            resources.Definition);

        Assert.Same(
            definition.DescriptorDefinition,
            resources.PropertyMap.DescriptorDefinition);

        Assert.Same(
            runtimeEndpoint,
            resources.Coordinator.RuntimeEndpoint);

        Assert.Same(
            resources.Coordinator,
            resources.Supervisor.Coordinator);

        Assert.Collection(
            resources.ResourcesAfterSupervision,
            resource =>
                Assert.Same(
                    resources.Coordinator,
                    resource));

        Assert.Equal(
            0,
            serialByteStreamFactory.OpenCallCount);

        await resources.SupervisionLifetime.DisposeAsync();

        foreach (
            IAsyncDisposable resource
            in resources.ResourcesAfterSupervision)
        {
            await resource.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreateSerial_ShouldNotConnectOrPublishEndpoint()
    {
        // Arrange
        CompactEndpointDefinition definition =
            CreateDefinition();

        var runtimeContext =
            new RuntimeContext();

        RuntimeEndpoint runtimeEndpoint =
            runtimeContext.CreateEndpoint(
                definition.DescriptorDefinition.Materialize(
                    new EndpointId(
                        "arduino-uno-01")));

        var serialByteStreamFactory =
            new CountingSerialByteStreamFactory();

        // Act
        CompactEndpointOperationalResources resources =
            CompactEndpointOperationalResources.CreateSerial(
                CreateConnectionDefinition(),
                definition,
                runtimeEndpoint,
                serialByteStreamFactory,
                new InMemoryCompactEndpointDefinitionRepository(
                    [
                        definition
                    ]),
                new DefaultRuntimeEndpointReconnectPolicy(),
                new CompactEndpointHealthProbeOptions(
                    TimeSpan.FromSeconds(
                        1),
                    TimeSpan.FromSeconds(
                        3)));

        // Assert
        Assert.Null(
            resources.Coordinator.ActiveConnection);

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            0,
            serialByteStreamFactory.OpenCallCount);

        await resources.SupervisionLifetime.DisposeAsync();

        foreach (
            IAsyncDisposable resource
            in resources.ResourcesAfterSupervision)
        {
            await resource.DisposeAsync();
        }
    }

    [Fact]
    public void CreateSerial_NullDefinition_ShouldThrow()
    {
        // Arrange
        var runtimeEndpoint =
            new RuntimeContext()
                .CreateEndpoint(
                    new EndpointDescriptorDefinition()
                        .Materialize(
                            new EndpointId(
                                "arduino-uno-01")));

        // Act
        void Act()
        {
            _ = CompactEndpointOperationalResources.CreateSerial(
                CreateConnectionDefinition(),
                null!,
                runtimeEndpoint,
                new CountingSerialByteStreamFactory(),
                new InMemoryCompactEndpointDefinitionRepository(
                    []),
                new DefaultRuntimeEndpointReconnectPolicy(),
                new CompactEndpointHealthProbeOptions(
                    TimeSpan.FromSeconds(
                        1),
                    TimeSpan.FromSeconds(
                        3)));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactEndpointDefinition CreateDefinition()
    {
        return new CompactEndpointDefinition(
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-validation"),
                version: 1),
            new EndpointDescriptorDefinition(),
            []);
    }

    private static SerialEndpointConnectionDefinition
        CreateConnectionDefinition()
    {
        return SerialEndpointConnectionDefinition.FromConfiguration(
            new SerialTransportOptions(
                "COM10",
                115200),
            new EndpointId(
                "arduino-uno-01"));
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
}