using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowInitialModeReadTests
{
    [Fact]
    public void ConnectedSelection_ReadsModeOnceAndRetainsConfirmedIndication()
    {
        RuntimeHostProfile profile = Profile();
        MultiHostClientSessionSnapshot snapshot = ConnectedSnapshot(
            profile,
            Guid.Parse("10d9ef89-267a-4de4-9ed1-04848635e6ab"));
        var coordinator = new FakeCoordinator(
            snapshot,
            Successful("CC"));
        MainWindowViewModel viewModel = Create(
            profile,
            coordinator);

        viewModel.SelectRuntimeHost(profile.ProfileId);
        viewModel.ApplyMultiHostSnapshot(snapshot);

        Assert.Single(coordinator.ReadTargets);
        Assert.Equal(
            "operating-mode",
            coordinator.ReadTargets[0].Target.PropertyId.Value);
        Assert.Equal(
            "CC",
            ActiveMode(viewModel));
    }

    [Fact]
    public void FailedInitialRead_IsNotRetriedDuringConnectedRefresh()
    {
        RuntimeHostProfile profile = Profile();
        MultiHostClientSessionSnapshot snapshot = ConnectedSnapshot(
            profile,
            Guid.Parse("20d9ef89-267a-4de4-9ed1-04848635e6ab"));
        var coordinator = new FakeCoordinator(
            snapshot,
            RemotePropertyOperationResult.Failed(
                RemotePropertyOperationStatus.EndpointUnavailable));
        MainWindowViewModel viewModel = Create(
            profile,
            coordinator);

        viewModel.SelectRuntimeHost(profile.ProfileId);
        viewModel.ApplyMultiHostSnapshot(snapshot);

        Assert.Single(coordinator.ReadTargets);
        Assert.Null(ActiveMode(viewModel));
    }

    [Fact]
    public void ReconnectedSession_AllowsOneNewInitialRead()
    {
        RuntimeHostProfile profile = Profile();
        Guid generation = Guid.Parse(
            "30d9ef89-267a-4de4-9ed1-04848635e6ab");
        MultiHostClientSessionSnapshot connected = ConnectedSnapshot(
            profile,
            generation);
        var coordinator = new FakeCoordinator(
            connected,
            RemotePropertyOperationResult.Failed(
                RemotePropertyOperationStatus.EndpointUnavailable),
            Successful("CC"));
        MainWindowViewModel viewModel = Create(
            profile,
            coordinator);
        viewModel.SelectRuntimeHost(profile.ProfileId);

        viewModel.ApplyMultiHostSnapshot(
            DisconnectedSnapshot(profile));
        viewModel.ApplyMultiHostSnapshot(connected);

        Assert.Equal(2, coordinator.ReadTargets.Count);
        Assert.Equal("CC", ActiveMode(viewModel));
    }

    [Fact]
    public void ExistingGoodMode_DoesNotStartInitialRead()
    {
        RuntimeHostProfile profile = Profile();
        MultiHostClientSessionSnapshot snapshot = ConnectedSnapshot(
            profile,
            Guid.Parse("40d9ef89-267a-4de4-9ed1-04848635e6ab"),
            authoritativeMode: "CW");
        var coordinator = new FakeCoordinator(
            snapshot,
            Successful("CC"));
        MainWindowViewModel viewModel = Create(
            profile,
            coordinator);

        viewModel.SelectRuntimeHost(profile.ProfileId);

        Assert.Empty(coordinator.ReadTargets);
        Assert.Equal("CW", ActiveMode(viewModel));
    }

    [Fact]
    public async Task CompletedPriorSessionRead_DoesNotReplaceReconnectedResult()
    {
        RuntimeHostProfile profile = Profile();
        Guid generation = Guid.Parse(
            "50d9ef89-267a-4de4-9ed1-04848635e6ab");
        MultiHostClientSessionSnapshot connected = ConnectedSnapshot(
            profile,
            generation);
        var priorCompletion = new TaskCompletionSource<RemotePropertyOperationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new FakeCoordinator(
            connected,
            priorCompletion.Task,
            Task.FromResult(Successful("CV")));
        MainWindowViewModel viewModel = Create(
            profile,
            coordinator);
        viewModel.SelectRuntimeHost(profile.ProfileId);

        viewModel.ApplyMultiHostSnapshot(
            DisconnectedSnapshot(profile));
        viewModel.ApplyMultiHostSnapshot(connected);
        Assert.Equal("CV", ActiveMode(viewModel));

        priorCompletion.SetResult(Successful("CC"));
        await WaitUntilAsync(() => coordinator.ReadTargets.Count == 2);
        await Task.Yield();

        Assert.Equal("CV", ActiveMode(viewModel));
    }

    private static MainWindowViewModel Create(
        RuntimeHostProfile profile,
        FakeCoordinator coordinator)
    {
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(
            new RuntimeHostProfileRegistry([profile]));
        viewModel.ConfigureMultiHostCoordinator(coordinator);
        return viewModel;
    }

    private static RuntimeHostProfile Profile() =>
        new(
            new RuntimeHostProfileId("desktop"),
            "Desktop Runtime Host",
            new RemoteRuntimeHostId("runtime-01"));

    private static MultiHostClientSessionSnapshot ConnectedSnapshot(
        RuntimeHostProfile profile,
        Guid generation,
        string? authoritativeMode = null)
    {
        var operatingMode = new PropertyDescriptor(
            new PropertyId("operating-mode"),
            DescriptorPath.Parse("Operating.Mode"),
            "Operating mode",
            new StringDataDescriptor())
        {
            AccessMode = PropertyAccessMode.Read
        };
        CommandDescriptor[] commands =
        [
            DeclaredCommandDescriptors.Mode("Mode.SelectConstantCurrent", "Select CC", "CC"),
            DeclaredCommandDescriptors.Mode("Mode.SelectConstantVoltage", "Select CV", "CV"),
            DeclaredCommandDescriptors.Mode("Mode.SelectConstantResistance", "Select CR", "CR"),
            DeclaredCommandDescriptors.Mode("Mode.SelectConstantPower", "Select CW", "CW"),
            DeclaredCommandDescriptors.Mode("Mode.SelectShortCircuit", "Select SHORT", "SHORT")
        ];
        var instrument = new InstrumentDescriptor(
            new InstrumentId("electronic-load-01"),
            "Electronic Load",
            new InstrumentKind("ElectronicLoad"))
        {
            Interface = new InstrumentInterface(
                properties: [operatingMode],
                commands: commands)
        };
        var attachment = new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(generation),
            new EndpointDescriptor(
                new EndpointId("kel-01"),
                [instrument]),
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready));
        var reducer = new RemoteObservationReducer();
        RemoteObservationState state = reducer.Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    profile.ExpectedRuntimeHostId,
                    RuntimeHostClientApiVersion.Current,
                    [attachment]),
                new RemoteObservationSequence(1)));
        if (authoritativeMode is not null)
        {
            state = reducer.Apply(
                state,
                new RemoteRuntimeHostObservation(
                    new RemoteObservationSequence(2),
                    attachment.Key,
                    new RemotePropertyValueChangedObservationPayload(
                        instrument.Id,
                        operatingMode.Id,
                        previousValue: null,
                        Value(authoritativeMode))));
        }

        return new MultiHostClientSessionSnapshot(
        [
            new RuntimeHostProfileSessionSnapshot(
                profile,
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Connected,
                    profile.ExpectedRuntimeHostId,
                    RuntimeHostClientApiVersion.Current),
                DateTimeOffset.UtcNow,
                state)
        ]);
    }

    private static MultiHostClientSessionSnapshot DisconnectedSnapshot(
        RuntimeHostProfile profile) =>
        new(
        [
            new RuntimeHostProfileSessionSnapshot(
                profile,
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Disconnected),
                DateTimeOffset.UtcNow)
        ]);

    private static RemotePropertyOperationResult Successful(
        string mode) =>
        RemotePropertyOperationResult.Successful(
            Value(mode));

    private static RemotePropertyValue Value(
        string mode) =>
        new(
            RemoteValue.FromString(mode),
            DateTimeOffset.UnixEpoch,
            RemotePropertyQuality.Good);

    private static string? ActiveMode(
        MainWindowViewModel viewModel) =>
        Assert.Single(
                Assert.Single(
                    viewModel.Endpoints)
                .Instruments)
            .ModeSelectionCommands
            .SingleOrDefault(command => command.IsActiveModeSelection)
            ?.ModeSelectionLabel;

    private static async Task WaitUntilAsync(
        Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private sealed class FakeCoordinator
        : IMultiHostClientSessionCoordinator
    {
        private readonly Queue<Task<RemotePropertyOperationResult>> results;

        public FakeCoordinator(
            MultiHostClientSessionSnapshot snapshot,
            params RemotePropertyOperationResult[] results)
            : this(
                snapshot,
                results.Select(
                    result => Task.FromResult(result))
                    .ToArray())
        {
        }

        public FakeCoordinator(
            MultiHostClientSessionSnapshot snapshot,
            params Task<RemotePropertyOperationResult>[] results)
        {
            Snapshot = snapshot;
            this.results = new Queue<Task<RemotePropertyOperationResult>>(results);
        }

        public event EventHandler? SnapshotChanged;
        public event EventHandler<RuntimeHostProfileEventOccurredEventArgs>? EventOccurred;

        public MultiHostClientSessionSnapshot Snapshot { get; }

        public List<RemoteRuntimeHostPropertyTarget> ReadTargets { get; } = [];

        public Task ConnectAsync(
            RuntimeHostProfileId profileId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DisconnectAsync(
            RuntimeHostProfileId profileId) =>
            Task.CompletedTask;

        public Task<RemotePropertyOperationResult> ReadPropertyAsync(
            RemoteRuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadTargets.Add(target);
            return results.Dequeue();
        }

        public Task<RemotePropertyOperationResult> WritePropertyAsync(
            RemoteRuntimeHostPropertyTarget target,
            RemoteValue requestedValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RemoteCommandOperationResult> ExecuteCommandAsync(
            RemoteRuntimeHostCommandExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
