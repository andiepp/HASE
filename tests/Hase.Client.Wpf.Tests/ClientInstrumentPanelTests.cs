using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientInstrumentPanelTests
{
    private const string PanelId = "rf-lab-signal-lab";

    [Fact]
    public void Registry_WithoutPanels_ShouldResolveNothing()
    {
        var registry = new ClientInstrumentPanelRegistry();

        Assert.Empty(registry.AvailablePanelIds);
        Assert.False(registry.TryResolve(PanelId, out _));
    }

    [Fact]
    public void Registry_ShouldResolveComposedPanelsByIdentifier()
    {
        var panel = new RecordingPanel(PanelId);
        var registry = new ClientInstrumentPanelRegistry([panel]);

        Assert.Equal([PanelId], registry.AvailablePanelIds);
        Assert.True(registry.TryResolve(PanelId, out IClientInstrumentPanel resolved));
        Assert.Same(panel, resolved);
        Assert.False(registry.TryResolve("other-panel", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registry_ShouldRejectEmptyPanelIdentifiers(string panelId)
    {
        Assert.Throws<ArgumentException>(
            () => new ClientInstrumentPanelRegistry([new RecordingPanel(panelId)]));
    }

    [Fact]
    public void Registry_ShouldRejectDuplicatePanelIdentifiers()
    {
        Assert.Throws<ArgumentException>(
            () => new ClientInstrumentPanelRegistry(
                [new RecordingPanel(PanelId), new RecordingPanel(PanelId)]));
    }

    [Fact]
    public void Projector_WithoutRegistry_ShouldOfferNoPanel()
    {
        IReadOnlyList<EndpointInventoryItemViewModel> endpoints =
            RuntimeHostInventoryProjector.Project(CreateState(PanelId));

        EndpointInventoryItemViewModel endpoint = Assert.Single(endpoints);
        Assert.Equal(PanelId, endpoint.Instruments.Single().PanelId);
        Assert.Null(endpoint.PanelId);
        Assert.False(endpoint.CanOpenPanel);
    }

    [Fact]
    public void Projector_DeclaredAndHostedPanel_ShouldOfferIt()
    {
        IReadOnlyList<EndpointInventoryItemViewModel> endpoints =
            RuntimeHostInventoryProjector.Project(
                CreateState(PanelId),
                availablePanelIds: new HashSet<string>(StringComparer.Ordinal)
                {
                    PanelId
                });

        EndpointInventoryItemViewModel endpoint = Assert.Single(endpoints);
        Assert.Equal(PanelId, endpoint.PanelId);
        Assert.Equal("rf-minilab-01", endpoint.PanelInstrumentId);
        Assert.True(endpoint.CanOpenPanel);
    }

    [Fact]
    public void Projector_DeclaredButNotHostedPanel_ShouldOfferNothing()
    {
        IReadOnlyList<EndpointInventoryItemViewModel> endpoints =
            RuntimeHostInventoryProjector.Project(
                CreateState(PanelId),
                availablePanelIds: new HashSet<string>(StringComparer.Ordinal)
                {
                    "some-other-panel"
                });

        EndpointInventoryItemViewModel endpoint = Assert.Single(endpoints);
        Assert.Null(endpoint.PanelId);
        Assert.False(endpoint.CanOpenPanel);
    }

    [Fact]
    public void Projector_UndeclaredInstrument_ShouldOfferNothing()
    {
        IReadOnlyList<EndpointInventoryItemViewModel> endpoints =
            RuntimeHostInventoryProjector.Project(
                CreateState(panelId: null),
                availablePanelIds: new HashSet<string>(StringComparer.Ordinal)
                {
                    PanelId
                });

        EndpointInventoryItemViewModel endpoint = Assert.Single(endpoints);
        Assert.Null(endpoint.Instruments.Single().PanelId);
        Assert.Null(endpoint.PanelId);
        Assert.False(endpoint.CanOpenPanel);
    }

    [Fact]
    public void Projector_UnreadyEndpoint_ShouldNotOfferItsPanel()
    {
        IReadOnlyList<EndpointInventoryItemViewModel> endpoints =
            RuntimeHostInventoryProjector.Project(
                CreateState(
                    PanelId,
                    RemoteEndpointConnectionState.Faulted),
                availablePanelIds: new HashSet<string>(StringComparer.Ordinal)
                {
                    PanelId
                });

        EndpointInventoryItemViewModel endpoint = Assert.Single(endpoints);
        Assert.Equal(PanelId, endpoint.PanelId);
        Assert.False(endpoint.CanOpenPanel);
    }

    [Fact]
    public async Task Operations_ShouldBindTargetsToTheAttachmentAndInstrument()
    {
        var attachment = new RemoteEndpointAttachmentKey(
            new EndpointId("rf-minilab-01"),
            new RemoteEndpointAttachmentGeneration(Guid.NewGuid()));
        RemotePropertyTarget? readTarget = null;
        RemotePropertyTarget? writeTarget = null;
        RemoteValue? writtenValue = null;
        RemoteCommandExecutionRequest? executed = null;

        var operations = new RuntimeHostInstrumentOperations(
            attachment,
            new InstrumentId("rf-minilab-01"),
            (target, _) =>
            {
                readTarget = target;
                return Task.FromResult(
                    RemotePropertyOperationResult.Failed(
                        RemotePropertyOperationStatus.EndpointUnavailable));
            },
            (target, value, _) =>
            {
                writeTarget = target;
                writtenValue = value;
                return Task.FromResult(
                    RemotePropertyOperationResult.Failed(
                        RemotePropertyOperationStatus.EndpointUnavailable));
            },
            (request, _) =>
            {
                executed = request;
                return Task.FromResult(
                    RemoteCommandOperationResult.Successful());
            });

        await operations.ReadAsync("sensor-level");
        await operations.WriteAsync("target-frequency", RemoteValue.FromNumeric(1e7));
        await operations.ExecuteAsync("Signal.ApplyCarrier");

        Assert.Same(attachment, operations.Attachment);
        Assert.Equal(new PropertyId("sensor-level"), readTarget!.PropertyId);
        Assert.Equal(attachment, readTarget.Attachment);
        Assert.Equal(new PropertyId("target-frequency"), writeTarget!.PropertyId);
        Assert.Equal(1e7, writtenValue!.NumericValue);
        Assert.Equal(
            DescriptorPath.Parse("Signal.ApplyCarrier"),
            executed!.Target.CommandPath);
        Assert.Equal(
            new InstrumentId("rf-minilab-01"),
            executed.Target.InstrumentId);
        Assert.Null(executed.Argument);
    }

    [Fact]
    public async Task Operations_ShouldRejectEmptyIdentifiers()
    {
        var operations = new RuntimeHostInstrumentOperations(
            new RemoteEndpointAttachmentKey(
                new EndpointId("rf-minilab-01"),
                new RemoteEndpointAttachmentGeneration(Guid.NewGuid())),
            new InstrumentId("rf-minilab-01"),
            (_, _) => throw new InvalidOperationException("unreachable"),
            (_, _, _) => throw new InvalidOperationException("unreachable"),
            (_, _) => throw new InvalidOperationException("unreachable"));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => operations.ReadAsync("  "));
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => operations.ExecuteAsync("  "));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => operations.WriteAsync("target-frequency", null!));
    }

    private static RemoteObservationState CreateState(
        string? panelId,
        RemoteEndpointConnectionState connectionState =
            RemoteEndpointConnectionState.Ready)
    {
        var instrument = new InstrumentDescriptor(
            new InstrumentId("rf-minilab-01"),
            "RF Signal Lab",
            new InstrumentKind("SignalGenerator"))
        {
            Presentation = panelId is null
                ? null
                : new InstrumentPresentation { PanelId = panelId }
        };
        var attachment = new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(
                Guid.Parse("6f2f6d1e-6a1f-4a2b-9a3d-2f5a6b7c8d9e")),
            new EndpointDescriptor(new EndpointId("rf-minilab-01"), [instrument]),
            new RemoteEndpointConnectionStatus(connectionState));

        return new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    new RemoteRuntimeHostId("runtime-01"),
                    RuntimeHostClientApiVersion.Current,
                    [attachment]),
                new RemoteObservationSequence(1)));
    }

    private sealed class RecordingPanel(string panelId) : IClientInstrumentPanel
    {
        public string PanelId => panelId;

        public List<ClientInstrumentPanelContext> Opened { get; } = [];

        public int CloseCount { get; private set; }

        public void Open(ClientInstrumentPanelContext context) =>
            Opened.Add(context);

        public void Close() => CloseCount++;
    }
}
