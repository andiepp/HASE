using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Simulation.Runtime.ByteBuffer;

namespace Hase.Simulation.Runtime.Tests.ByteBuffer;

public sealed class ByteBufferNorthboundIntegrationTests
{
    [Fact]
    public async Task EventTriggerCommands_PublishAllTypedNorthboundOccurrences()
    {
        var endpointId =
            new EndpointId(
                "simulation-byte-buffer-validation");
        string identityFilePath =
            Path.Combine(
                Path.GetTempPath(),
                $"hase-byte-buffer-events-{Guid.NewGuid():N}.json");

        try
        {
            var context =
                new RuntimeContext();
            var attachmentService =
                new InProcessEndpointAttachmentService(
                    context);

            await using var inventory =
                new RuntimeEndpointAttachmentInventory(
                    attachmentService);

            await inventory.AttachAsync(
                CreateRequest(
                    endpointId));

            await using RuntimeHostNorthboundSnapshotComposition composition =
                await RuntimeHostNorthboundSnapshotComposition
                    .CreateFileBackedAsync(
                        inventory,
                        identityFilePath,
                        new RuntimeHostId(
                            "byte-buffer-event-integration-host"));

            PublishedRuntimeEndpointSnapshot endpoint =
                Assert.Single(
                    composition.SnapshotProvider
                        .Capture()
                        .Endpoints);

            await using RuntimeHostObservationSubscription subscription =
                await composition.ObservationService
                    .OpenSubscriptionAsync(
                        new RuntimeHostObservationSubscriptionOptions());

            (
                DescriptorPath CommandPath,
                DescriptorPath EventPath,
                object? Value)[] cases =
            [
                (
                    ByteBufferDescriptorFactory.EmitNoPayloadCommandPath,
                    ByteBufferDescriptorFactory.NoPayloadEventPath,
                    null),
                (
                    ByteBufferDescriptorFactory.EmitBooleanCommandPath,
                    ByteBufferDescriptorFactory.BooleanEventPath,
                    true),
                (
                    ByteBufferDescriptorFactory.EmitNumericCommandPath,
                    ByteBufferDescriptorFactory.NumericEventPath,
                    23.5),
                (
                    ByteBufferDescriptorFactory.EmitStringCommandPath,
                    ByteBufferDescriptorFactory.StringEventPath,
                    "HASE event validation"),
                (
                    ByteBufferDescriptorFactory.EmitByteArrayCommandPath,
                    ByteBufferDescriptorFactory.ByteArrayEventPath,
                    new ByteArrayValue(
                        new byte[]
                        {
                            0x01,
                            0xAB,
                            0x00,
                            0xFF
                        }))
            ];

            using var observationTimeout =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(
                        5));

            await using IAsyncEnumerator<RuntimeHostObservation> observations =
                subscription.ReadAllAsync(
                        observationTimeout.Token)
                    .GetAsyncEnumerator(
                        observationTimeout.Token);

            foreach (var validationCase in cases)
            {
                var target =
                    new RuntimeHostCommandTarget(
                        endpointId,
                        endpoint.Generation,
                        ByteBufferDescriptorFactory.InstrumentId,
                        validationCase.CommandPath);

                RuntimeHostCommandOperationResult result =
                    await composition.CommandService.ExecuteAsync(
                        target,
                        null);

                Assert.True(
                    result.IsSuccess);
                Assert.Null(
                    result.ReturnValue);
                Assert.True(
                    await observations.MoveNextAsync());

                RuntimeHostObservation observation =
                    observations.Current;
                Assert.Equal(
                    RuntimeHostObservationKind.EventOccurred,
                    observation.Kind);
                Assert.Equal(
                    endpointId,
                    observation.EndpointId);
                Assert.Equal(
                    endpoint.Generation,
                    observation.AttachmentGeneration);

                RuntimeHostEventOccurredObservationPayload occurrence =
                    Assert.IsType<
                        RuntimeHostEventOccurredObservationPayload>(
                        observation.Payload);
                Assert.Equal(
                    ByteBufferDescriptorFactory.InstrumentId,
                    occurrence.InstrumentId);
                Assert.Equal(
                    validationCase.EventPath,
                    occurrence.EventPath);
                Assert.Equal(
                    validationCase.Value,
                    occurrence.Value);
            }
        }
        finally
        {
            if (File.Exists(
                    identityFilePath))
            {
                File.Delete(
                    identityFilePath);
            }
        }
    }

    [Fact]
    public async Task CommandExecution_UpdatesCachedValueAndPublishesObservation()
    {
        var endpointId =
            new EndpointId(
                "simulation-byte-buffer-validation");
        string identityFilePath =
            Path.Combine(
                Path.GetTempPath(),
                $"hase-byte-buffer-{Guid.NewGuid():N}.json");

        try
        {
            var context =
                new RuntimeContext();
            var attachmentService =
                new InProcessEndpointAttachmentService(
                    context);

            await using var inventory =
                new RuntimeEndpointAttachmentInventory(
                    attachmentService);

            await inventory.AttachAsync(
                CreateRequest(
                    endpointId));

            await using RuntimeHostNorthboundSnapshotComposition composition =
                await RuntimeHostNorthboundSnapshotComposition
                    .CreateFileBackedAsync(
                        inventory,
                        identityFilePath,
                        new RuntimeHostId(
                            "byte-buffer-integration-host"));

            PublishedRuntimeEndpointSnapshot endpoint =
                Assert.Single(
                    composition.SnapshotProvider
                        .Capture()
                        .Endpoints);

            var propertyTarget =
                new RuntimeHostPropertyTarget(
                    endpointId,
                    endpoint.Generation,
                    ByteBufferDescriptorFactory.InstrumentId,
                    ByteBufferDescriptorFactory.ValuePropertyId);

            var commandTarget =
                new RuntimeHostCommandTarget(
                    endpointId,
                    endpoint.Generation,
                    ByteBufferDescriptorFactory.InstrumentId,
                    ByteBufferDescriptorFactory.ReplaceCommandPath);

            await using RuntimeHostObservationSubscription subscription =
                await composition.ObservationService
                    .OpenSubscriptionAsync(
                        new RuntimeHostObservationSubscriptionOptions());

            var payload =
                new ByteArrayValue(
                    new byte[]
                    {
                        0x00,
                        0x7f,
                        0xff
                    });

            RuntimeHostCommandOperationResult commandResult =
                await composition.CommandService.ExecuteAsync(
                    commandTarget,
                    payload);

            Assert.True(
                commandResult.IsSuccess);
            Assert.Equal(
                payload,
                commandResult.ReturnValue);

            RuntimeHostCachedPropertyResult cachedResult =
                composition.PropertyService.GetCached(
                    propertyTarget);

            Assert.True(
                cachedResult.IsSuccess);
            Assert.Equal(
                payload,
                cachedResult.Snapshot?.CurrentValue?.Value);

            using var observationTimeout =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(
                        5));

            RuntimeHostObservation? observation =
                null;

            await foreach (
                RuntimeHostObservation candidate
                in subscription.ReadAllAsync(
                    observationTimeout.Token))
            {
                observation =
                    candidate;
                break;
            }

            Assert.NotNull(
                observation);
            Assert.Equal(
                endpointId,
                observation.EndpointId);
            Assert.Equal(
                endpoint.Generation,
                observation.AttachmentGeneration);

            RuntimeHostPropertyValueChangedObservationPayload change =
                Assert.IsType<
                    RuntimeHostPropertyValueChangedObservationPayload>(
                    observation.Payload);

            Assert.Equal(
                ByteBufferDescriptorFactory.InstrumentId,
                change.InstrumentId);
            Assert.Equal(
                ByteBufferDescriptorFactory.ValuePropertyId,
                change.PropertyId);
            Assert.Equal(
                payload,
                change.CurrentValue.Value);
        }
        finally
        {
            if (File.Exists(
                    identityFilePath))
            {
                File.Delete(
                    identityFilePath);
            }
        }
    }

    private static EndpointAttachmentRequest CreateRequest(
        EndpointId endpointId)
    {
        var simulation =
            new ByteBufferSimulation();

        return new EndpointAttachmentRequest(
            new InProcessEndpointConnectionDefinition(
                new EndpointDescriptor(
                    endpointId,
                    [
                        ByteBufferDescriptorFactory.CreateDescriptor()
                    ]),
                runtimeInstrument =>
                    new ByteBufferInstrumentExecutor(
                        simulation,
                        runtimeInstrument)),
            InProcessEndpointDescriptorSource.Instance);
    }
}
