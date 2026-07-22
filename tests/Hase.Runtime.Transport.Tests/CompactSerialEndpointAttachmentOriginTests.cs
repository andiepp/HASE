using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactSerialEndpointAttachmentOriginTests
{
    [Theory]
    [InlineData(
        false)]
    [InlineData(
        true)]
    public async Task AttachAsync_ConfiguredOrDiscoveredDefinition_ShouldUseSameBootstrapPath(
        bool useDiscoveredDefinition)
    {
        // Arrange
        var expectedException =
            new IOException(
                "Controlled bootstrap boundary.");

        SerialEndpointConnectionDefinition? receivedDefinition =
            null;

        int resolverCallCount =
            0;

        int resourceFactoryCallCount =
            0;

        var service =
            new CompactSerialEndpointAttachmentService(
                new RuntimeContext(),
                (
                    connectionDefinition,
                    cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    receivedDefinition =
                        connectionDefinition;

                    throw expectedException;
                },
                (
                    bootstrapResult,
                    cancellationToken) =>
                {
                    resolverCallCount++;

                    throw new InvalidOperationException(
                        "Operational definition resolution was not expected.");
                },
                (
                    connectionDefinition,
                    definition,
                    runtimeEndpoint) =>
                {
                    resourceFactoryCallCount++;

                    throw new InvalidOperationException(
                        "Operational resource creation was not expected.");
                });

        SerialEndpointConnectionDefinition connectionDefinition =
            useDiscoveredDefinition
                ? CreateDiscoveredDefinition()
                : CreateConfiguredDefinition();

        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                HostRepositoryDescriptorSource.Instance);

        // Act
        Task Act()
        {
            return service.AttachAsync(
                request);
        }

        // Assert
        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Same(
            connectionDefinition,
            receivedDefinition);

        Assert.Equal(
            useDiscoveredDefinition
                ? EndpointConnectionOrigin.Discovered
                : EndpointConnectionOrigin.Configured,
            receivedDefinition!.Origin);

        Assert.Equal(
            "COM10",
            receivedDefinition.TransportOptions.PortName);

        Assert.Equal(
            115200,
            receivedDefinition.TransportOptions.BaudRate);

        Assert.Equal(
            0,
            resolverCallCount);

        Assert.Equal(
            0,
            resourceFactoryCallCount);
    }

    private static SerialEndpointConnectionDefinition
        CreateConfiguredDefinition()
    {
        return SerialEndpointConnectionDefinition.FromConfiguration(
            new SerialTransportOptions(
                "COM10",
                115200),
            new EndpointId(
                "arduino-uno-01"));
    }

    private static SerialEndpointConnectionDefinition
        CreateDiscoveredDefinition()
    {
        var verifiedEndpoint =
            new VerifiedUsbSerialEndpoint(
                new UsbSerialEndpointCandidate(
                    "COM10",
                    vendorId: 0x2341,
                    productId: 0x0043,
                    productName: "Arduino Uno",
                    manufacturerName: "Arduino LLC",
                    serialNumber: "75836333537351D06110"),
                new EndpointId(
                    "arduino-uno-01"),
                new DescriptorReference(
                    new DescriptorId(
                        "arduino-uno-validation"),
                    version: 1),
                new EndpointDescriptorDefinition());

        return SerialEndpointConnectionDefinition.FromVerifiedEndpoint(
            verifiedEndpoint,
            new UsbSerialEndpointDiscoveryOptions(
                115200,
                TimeSpan.FromSeconds(
                    3)));
    }
}