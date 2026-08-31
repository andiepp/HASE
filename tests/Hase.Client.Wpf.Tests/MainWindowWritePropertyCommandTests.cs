using Hase.Client;
using Hase.Client.Configuration;
using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

/// <summary>
/// The Write button carries both a command and its own enablement binding,
/// and the host combines them so that the command decides. Nothing re-queries
/// a command while the operator types, so whether the typed value is valid
/// must not live in the command's predicate — only in the binding, which
/// follows the item directly.
/// </summary>
public sealed class MainWindowWritePropertyCommandTests
{
    [Fact]
    public void TheCommand_ShouldNotDependOnTheTypedValue()
    {
        MainWindowViewModel viewModel = CreateConnected();
        PropertyInventoryItemViewModel property = TargetProperty(viewModel);

        // Empty, then invalid, then valid: the command's answer must not move,
        // because no re-query reaches it between keystrokes.
        property.RequestedValueText = string.Empty;
        Assert.True(viewModel.WritePropertyCommand.CanExecute(property));

        property.RequestedValueText = "not a number";
        Assert.True(viewModel.WritePropertyCommand.CanExecute(property));

        property.RequestedValueText = "12";
        Assert.True(viewModel.WritePropertyCommand.CanExecute(property));
    }

    [Fact]
    public void TheEnablementBinding_ShouldFollowTheTypedValue()
    {
        MainWindowViewModel viewModel = CreateConnected();
        PropertyInventoryItemViewModel property = TargetProperty(viewModel);

        // This is what the button binds, and it updates on every keystroke.
        property.RequestedValueText = string.Empty;
        Assert.False(property.CanSubmitWrite);

        property.RequestedValueText = "12";
        Assert.True(property.CanSubmitWrite);

        property.RequestedValueText = "999";
        Assert.False(property.CanSubmitWrite);

        property.RequestedValueText = "12";
        Assert.True(property.CanSubmitWrite);
    }

    [Fact]
    public void TheEnablementBinding_ShouldAnnounceEachChange()
    {
        MainWindowViewModel viewModel = CreateConnected();
        PropertyInventoryItemViewModel property = TargetProperty(viewModel);
        var announced = new List<bool>();
        property.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(property.CanSubmitWrite))
            {
                announced.Add(property.CanSubmitWrite);
            }
        };

        property.RequestedValueText = "12";
        property.RequestedValueText = "999";

        // Without these the button could not follow the typed value at all.
        Assert.Equal([true, false], announced);
    }

    [Fact]
    public void ARemovedWriteAccess_ShouldStillDisableTheCommand()
    {
        MainWindowViewModel viewModel = CreateConnected();
        PropertyInventoryItemViewModel property = ReadOnlyProperty(viewModel);

        // The coarse gates the predicate does keep must still hold.
        Assert.False(property.CanWrite);
        Assert.False(viewModel.WritePropertyCommand.CanExecute(property));
    }

    [Fact]
    public void ADisconnectedWorkspace_ShouldDisableTheCommand()
    {
        MainWindowViewModel viewModel = CreateConnected();
        PropertyInventoryItemViewModel property = TargetProperty(viewModel);
        property.RequestedValueText = "12";

        RuntimeHostProfile profile = Profile("first", "host-01");
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Disconnected)]));

        Assert.False(viewModel.WritePropertyCommand.CanExecute(property));
    }

    private static MainWindowViewModel CreateConnected(
        PropertyAccessMode accessMode = PropertyAccessMode.ReadWrite)
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        var viewModel = new MainWindowViewModel();

        var snapshot = new MultiHostClientSessionSnapshot([
            Session(
                profile,
                RuntimeHostClientSessionState.Connected,
                PropertyState("host-01", accessMode))]);

        // The application configures both, so the workspace has a session
        // controller and a coordinator, as it does when driving several hosts.
        viewModel.Configure(new StubSessionController(), new StubFilePicker());
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([profile]));
        viewModel.ConfigureMultiHostCoordinator(new StubCoordinator(snapshot));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        viewModel.ApplyMultiHostSnapshot(snapshot);
        return viewModel;
    }

    private static RuntimeHostProfile Profile(string id, string host) =>
        new(new RuntimeHostProfileId(id), id, new RemoteRuntimeHostId(host));

    private static RuntimeHostProfileSessionSnapshot Session(
        RuntimeHostProfile profile,
        RuntimeHostClientSessionState state,
        RemoteObservationState? currentState = null)
    {
        RuntimeHostClientSessionStatus status =
            state is RuntimeHostClientSessionState.Connected
                or RuntimeHostClientSessionState.Reconnecting
                ? new RuntimeHostClientSessionStatus(
                    state,
                    profile.ExpectedRuntimeHostId,
                    RuntimeHostClientApiVersion.Current)
                : new RuntimeHostClientSessionStatus(state);

        return new RuntimeHostProfileSessionSnapshot(
            profile,
            status,
            DateTimeOffset.UtcNow,
            currentState);
    }

    private static PropertyInventoryItemViewModel TargetProperty(
        MainWindowViewModel viewModel) =>
        Assert.Single(
            Assert.Single(Assert.Single(viewModel.Endpoints).Instruments)
                .Properties);

    private static PropertyInventoryItemViewModel ReadOnlyProperty(
        MainWindowViewModel viewModel)
    {
        MainWindowViewModel readOnly = CreateConnected(PropertyAccessMode.Read);
        return TargetProperty(readOnly);
    }

    private static RemoteObservationState PropertyState(
        string host,
        PropertyAccessMode accessMode)
    {
        var property = new PropertyDescriptor(
            new PropertyId("target-current"),
            DescriptorPath.Parse("Target.Current"),
            "Target current",
            new NumericDataDescriptor(
                Quantities.Current,
                Units.Ampere,
                new ValueRange(0, 30)))
        {
            AccessMode = accessMode
        };
        var instrument = new InstrumentDescriptor(
            new InstrumentId("electronic-load-01"),
            "Electronic Load",
            new InstrumentKind("ElectronicLoad"))
        {
            Interface = new InstrumentInterface(properties: [property])
        };
        var attachment = new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(
                Guid.Parse("6b1c9f2e-6d3a-4f52-9f0e-2c7a1b4d8e90")),
            new EndpointDescriptor(new EndpointId("kel-01"), [instrument]),
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready));

        return new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    new RemoteRuntimeHostId(host),
                    RuntimeHostClientApiVersion.Current,
                    [attachment]),
                new RemoteObservationSequence(0)));
    }

    private sealed class StubSessionController
        : IRuntimeHostClientSessionController
    {
        public Task ConnectAsync(
            string configurationFilePath,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DisconnectAsync() =>
            Task.CompletedTask;

        public Task<RemotePropertyOperationResult> ReadPropertyAsync(
            RemotePropertyTarget target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.EndpointUnavailable));

        public Task<RemotePropertyOperationResult> WritePropertyAsync(
            RemotePropertyTarget target,
            RemoteValue requestedValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.EndpointUnavailable));

        public Task<RemoteCommandOperationResult> ExecuteCommandAsync(
            RemoteCommandExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RemoteCommandOperationResult.Failed(
                    RemoteCommandOperationStatus.EndpointRejected,
                    "stub"));

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }

    private sealed class StubFilePicker : IClientConfigurationFilePicker
    {
        public string? PickConfigurationFile() =>
            null;
    }

    private sealed class StubCoordinator : IMultiHostClientSessionCoordinator
    {
        public StubCoordinator(MultiHostClientSessionSnapshot snapshot) =>
            Snapshot = snapshot;

        public event EventHandler? SnapshotChanged;

        public event EventHandler<RuntimeHostProfileEventOccurredEventArgs>?
            EventOccurred;

        public MultiHostClientSessionSnapshot Snapshot { get; }

        public Task ConnectAsync(
            RuntimeHostProfileId profileId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DisconnectAsync(RuntimeHostProfileId profileId) =>
            Task.CompletedTask;

        public Task<RemotePropertyOperationResult> ReadPropertyAsync(
            RemoteRuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.EndpointUnavailable));

        public Task<RemotePropertyOperationResult> WritePropertyAsync(
            RemoteRuntimeHostPropertyTarget target,
            RemoteValue requestedValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.EndpointUnavailable));

        public Task<RemoteCommandOperationResult> ExecuteCommandAsync(
            RemoteRuntimeHostCommandExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RemoteCommandOperationResult.Failed(
                    RemoteCommandOperationStatus.EndpointRejected,
                    "stub"));

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
