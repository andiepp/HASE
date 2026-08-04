using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostKel103AttachmentSetTests
{
    [Fact]
    public async Task OpenAsync_EmptyComposition_ShouldOpenNothing()
    {
        var factory = new RecordingFactory();

        await using DesktopRuntimeHostKel103AttachmentSet set =
            await DesktopRuntimeHostKel103AttachmentSet.OpenAsync([], [], factory);

        Assert.Equal(0, set.Count);
        Assert.Empty(factory.Operations);
    }

    [Fact]
    public async Task OpenAsync_MultipleProfiles_ShouldPreserveOrderAndFixedFraming()
    {
        var factory = new RecordingFactory();
        DesktopRuntimeHostKel103SerialEndpointProfile[] profiles =
            [Profile("first", "target-one"), Profile("second", "target-two")];

        await using DesktopRuntimeHostKel103AttachmentSet set =
            await DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                profiles,
                profiles.Select(Plan).ToArray(),
                factory);

        Assert.Equal(2, set.Count);
        Assert.Equal(new[] { "open:first", "open:second" }, factory.Operations);
        Assert.Equal(
            new[] { "target-one", "target-two" },
            factory.Options.Select(options => options.PortName));
        Assert.All(factory.Options, options =>
        {
            Assert.Equal(115200, options.BaudRate);
            Assert.Equal(8, options.DataBits);
            Assert.Equal(SerialParity.None, options.Parity);
            Assert.Equal(SerialStopBits.One, options.StopBits);
            Assert.Equal(SerialHandshake.None, options.Handshake);
        });
        Assert.DoesNotContain("target-one", set.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("target-two", set.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAsync_ForwardsExactVersionFourPlanDefinition()
    {
        var factory = new RecordingFactory();
        DesktopRuntimeHostKel103SerialEndpointProfile profile =
            new(
                "controlled",
                Kel103ControlledSetpointDefinition.Reference.Id.Value,
                Kel103ControlledSetpointDefinition.Reference.Version,
                "external-target",
                115200);
        var plan = new DesktopRuntimeHostKel103EndpointPlan(
            new EndpointId(profile.ExpectedEndpointId),
            Kel103ControlledSetpointDefinition.EndpointDefinition);

        await using DesktopRuntimeHostKel103AttachmentSet set =
            await DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                [profile],
                [plan],
                factory);

        Assert.Equal(1, set.Count);
        Assert.Same(
            Kel103ControlledSetpointDefinition.EndpointDefinition,
            Assert.Single(factory.Definitions));
    }

    [Fact]
    public async Task OpenAsync_DifferentProfileAndPlanCounts_ShouldRejectBeforeOpen()
    {
        var factory = new RecordingFactory();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                [Profile("first", "target-one")],
                [],
                factory));

        Assert.Empty(factory.Operations);
    }

    [Fact]
    public async Task OpenAsync_MismatchedIdentity_ShouldRejectBeforeOpen()
    {
        var factory = new RecordingFactory();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                [Profile("first", "target-one")],
                [Plan(Profile("second", "target-two"))],
                factory));

        Assert.Empty(factory.Operations);
    }

    [Fact]
    public async Task OpenAsync_MismatchedProfileAndPlanDefinitionRejectsBeforeOpen()
    {
        var factory = new RecordingFactory();
        DesktopRuntimeHostKel103SerialEndpointProfile profile =
            Profile("first", "external-target");
        var plan = new DesktopRuntimeHostKel103EndpointPlan(
            new EndpointId(profile.ExpectedEndpointId),
            Kel103ControlledSetpointDefinition.EndpointDefinition);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                [profile],
                [plan],
                factory));

        Assert.Empty(factory.Operations);
    }

    [Fact]
    public async Task OpenAsync_PartialFailure_ShouldDisposeEarlierAttachmentsInReverseOrder()
    {
        const string sensitiveTarget = "sensitive-target";
        var factory = new RecordingFactory(
            failOnOpenNumber: 3,
            sensitiveTarget: sensitiveTarget);
        DesktopRuntimeHostKel103SerialEndpointProfile[] profiles =
        [
            Profile("first", "target-one"),
            Profile("second", "target-two"),
            Profile("third", sensitiveTarget)
        ];

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                profiles,
                profiles.Select(Plan).ToArray(),
                factory));

        Assert.Equal(
            new[] { "open:first", "open:second", "open:third", "dispose:second", "dispose:first" },
            factory.Operations);
        Assert.DoesNotContain(sensitiveTarget, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAsync_Cancellation_ShouldDisposeEarlierAttachmentsInReverseOrder()
    {
        var factory = new RecordingFactory(cancelOnOpenNumber: 2);
        DesktopRuntimeHostKel103SerialEndpointProfile[] profiles =
            [Profile("first", "target-one"), Profile("second", "target-two")];

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                profiles,
                profiles.Select(Plan).ToArray(),
                factory));

        Assert.Equal(
            new[] { "open:first", "open:second", "dispose:first" },
            factory.Operations);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedAndConcurrent_ShouldDisposeOnceInReverseOrder()
    {
        var factory = new RecordingFactory();
        DesktopRuntimeHostKel103SerialEndpointProfile[] profiles =
            [Profile("first", "target-one"), Profile("second", "target-two")];
        DesktopRuntimeHostKel103AttachmentSet set =
            await DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                profiles,
                profiles.Select(Plan).ToArray(),
                factory);

        await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => set.DisposeAsync().AsTask()));
        await set.DisposeAsync();

        Assert.Equal(
            new[] { "open:first", "open:second", "dispose:second", "dispose:first" },
            factory.Operations);
    }

    [Fact]
    public async Task DisposeAsync_MultipleFailures_ShouldAggregateWithoutTargetLeak()
    {
        const string sensitiveTarget = "sensitive-target";
        var factory = new RecordingFactory(failDisposal: true, sensitiveTarget: sensitiveTarget);
        DesktopRuntimeHostKel103SerialEndpointProfile[] profiles =
            [Profile("first", sensitiveTarget), Profile("second", "target-two")];
        DesktopRuntimeHostKel103AttachmentSet set =
            await DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                profiles,
                profiles.Select(Plan).ToArray(),
                factory);

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => set.DisposeAsync().AsTask());

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.DoesNotContain(sensitiveTarget, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(
            new[] { "open:first", "open:second", "dispose:second", "dispose:first" },
            factory.Operations);
    }

    [Fact]
    public async Task OpenAsync_PrimaryAndCleanupFailures_ShouldAggregateInStableOrder()
    {
        const string sensitiveTarget = "sensitive-target";
        var factory = new RecordingFactory(
            failOnOpenNumber: 3,
            sensitiveTarget: sensitiveTarget,
            failDisposal: true);
        DesktopRuntimeHostKel103SerialEndpointProfile[] profiles =
        [
            Profile("first", "target-one"),
            Profile("second", "target-two"),
            Profile("third", sensitiveTarget)
        ];

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() =>
            DesktopRuntimeHostKel103AttachmentSet.OpenAsync(
                profiles,
                profiles.Select(Plan).ToArray(),
                factory));

        Assert.Equal(3, exception.InnerExceptions.Count);
        Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
        Assert.DoesNotContain(sensitiveTarget, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(
            new[] { "open:first", "open:second", "open:third", "dispose:second", "dispose:first" },
            factory.Operations);
    }

    private static DesktopRuntimeHostKel103SerialEndpointProfile Profile(
        string endpointId,
        string serialTarget) =>
        new(
            endpointId,
            Kel103ReadOnlyMeasurementDefinition.Reference.Id.Value,
            Kel103ReadOnlyMeasurementDefinition.Reference.Version,
            serialTarget,
            115200);

    private static DesktopRuntimeHostKel103EndpointPlan Plan(
        DesktopRuntimeHostKel103SerialEndpointProfile profile) =>
        new(
            new EndpointId(profile.ExpectedEndpointId),
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition);

    private sealed class RecordingFactory(
        int? failOnOpenNumber = null,
        string sensitiveTarget = "unused-sensitive-target",
        int? cancelOnOpenNumber = null,
        bool failDisposal = false)
        : IDesktopRuntimeHostKel103AttachmentFactory
    {
        private int openCount;

        public List<string> Operations { get; } = [];
        public List<SerialTransportOptions> Options { get; } = [];
        public List<EndpointDescriptorDefinition> Definitions { get; } = [];

        public Task<IDesktopRuntimeHostKel103Attachment> OpenAsync(
            EndpointId endpointId,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken = default)
            => OpenCoreAsync(
                endpointId,
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition,
                serialOptions,
                cancellationToken);

        public Task<IDesktopRuntimeHostKel103Attachment> OpenAsync(
            EndpointId endpointId,
            EndpointDescriptorDefinition definition,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken = default)
            => OpenCoreAsync(endpointId, definition, serialOptions, cancellationToken);

        private Task<IDesktopRuntimeHostKel103Attachment> OpenCoreAsync(
            EndpointId endpointId,
            EndpointDescriptorDefinition definition,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken)
        {
            openCount++;
            Operations.Add($"open:{endpointId.Value}");
            Options.Add(serialOptions);
            Definitions.Add(definition);

            if (openCount == cancelOnOpenNumber)
            {
                throw new OperationCanceledException(
                    $"Cancelled for {sensitiveTarget}.",
                    cancellationToken);
            }

            if (openCount == failOnOpenNumber)
            {
                throw new InvalidOperationException(
                    $"Opening {sensitiveTarget} failed.");
            }

            return Task.FromResult<IDesktopRuntimeHostKel103Attachment>(
                new RecordingAttachment(
                    endpointId.Value,
                    Operations,
                    failDisposal,
                    sensitiveTarget));
        }
    }

    private sealed class RecordingAttachment(
        string endpointId,
        List<string> operations,
        bool failDisposal,
        string sensitiveTarget) : IDesktopRuntimeHostKel103Attachment
    {
        public RuntimeEndpoint RuntimeEndpoint { get; } =
            new RuntimeContext().CreateEndpoint(
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(
                    new EndpointId(endpointId)));

        public IEndpointAttachmentPropertyOperations PropertyOperations { get; } =
            new ThrowingPropertyOperations();

        public ValueTask DisposeAsync()
        {
            operations.Add($"dispose:{endpointId}");

            if (failDisposal)
            {
                throw new InvalidOperationException(
                    $"Disposal for {sensitiveTarget} failed.");
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingPropertyOperations
        : IEndpointAttachmentPropertyOperations
    {
        public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
