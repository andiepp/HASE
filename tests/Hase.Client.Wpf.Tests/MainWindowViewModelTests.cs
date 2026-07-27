using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_ShouldExposeDisconnectedShell()
    {
        var viewModel =
            new MainWindowViewModel();

        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            viewModel.SessionStatus.State);
        Assert.Equal(
            "Disconnected",
            viewModel.SessionState);
        Assert.Equal(
            "Not connected",
            viewModel.RuntimeHostId);
        Assert.Equal(
            "Not available",
            viewModel.ApiVersion);
        Assert.True(
            viewModel.CanConnect);
        Assert.False(
            viewModel.CanDisconnect);
        Assert.False(
            viewModel.IsOperational);
        Assert.False(
            viewModel.IsStale);
    }

    [Fact]
    public void ApplySessionStatus_Connected_ShouldExposeIdentityAndVersion()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));

        Assert.Equal(
            "Connected",
            viewModel.SessionState);
        Assert.Equal(
            "runtime-host-01",
            viewModel.RuntimeHostId);
        Assert.Equal(
            "1.0",
            viewModel.ApiVersion);
        Assert.False(
            viewModel.CanConnect);
        Assert.True(
            viewModel.CanDisconnect);
        Assert.True(
            viewModel.IsOperational);
        Assert.False(
            viewModel.IsStale);
    }

    [Fact]
    public void ApplySessionStatus_Reconnecting_ShouldRetainStaleBaseline()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Reconnecting));

        Assert.Equal(
            "runtime-host-01",
            viewModel.RuntimeHostId);
        Assert.Equal(
            "1.0",
            viewModel.ApiVersion);
        Assert.False(
            viewModel.CanConnect);
        Assert.True(
            viewModel.CanDisconnect);
        Assert.False(
            viewModel.IsOperational);
        Assert.True(
            viewModel.IsStale);
    }

    [Fact]
    public void ApplySessionStatus_Faulted_ShouldAllowDeliberateReconnect()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Faulted));

        Assert.True(
            viewModel.CanConnect);
        Assert.False(
            viewModel.CanDisconnect);
        Assert.False(
            viewModel.IsOperational);
        Assert.False(
            viewModel.IsStale);
    }

    [Fact]
    public void ApplySessionStatus_ShouldRaiseDependentProperties()
    {
        var viewModel =
            new MainWindowViewModel();
        var changedProperties =
            new List<string?>();
        viewModel.PropertyChanged +=
            (_, eventArgs) =>
                changedProperties.Add(
                    eventArgs.PropertyName);

        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));

        Assert.Contains(
            nameof(MainWindowViewModel.SessionStatus),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.SessionState),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.RuntimeHostId),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.ApiVersion),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.CanConnect),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.CanDisconnect),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.IsOperational),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.IsStale),
            changedProperties);
    }

    [Fact]
    public void ApplySessionStatus_Null_ShouldThrow()
    {
        var viewModel =
            new MainWindowViewModel();

        Assert.Throws<ArgumentNullException>(
            () =>
                viewModel.ApplySessionStatus(
                    null!));
    }

    [Fact]
    public void ApplyObservationState_ShouldExposeStateAndEndpointCount()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplyObservationState(
            RemoteObservationState.Empty);

        Assert.Same(
            RemoteObservationState.Empty,
            viewModel.CurrentState);
        Assert.Equal(
            0,
            viewModel.EndpointCount);
    }

    [Fact]
    public void ApplyObservationState_Null_ShouldThrow()
    {
        var viewModel =
            new MainWindowViewModel();

        Assert.Throws<ArgumentNullException>(
            () =>
                viewModel.ApplyObservationState(
                    null!));
    }

    [Fact]
    public async Task ConnectAsync_SelectedConfiguration_ShouldUseController()
    {
        var controller =
            new StubController();
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                @"C:\HASE\client.json");

        await viewModel.ConnectAsync();

        Assert.Equal(
            @"C:\HASE\client.json",
            controller.ConfigurationFilePath);
        Assert.Null(
            viewModel.FailureMessage);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task ConnectAsync_CancelledSelection_ShouldNotConnect()
    {
        var controller =
            new StubController();
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                null);

        await viewModel.ConnectAsync();

        Assert.Null(
            controller.ConfigurationFilePath);
    }

    [Fact]
    public async Task ConnectAsync_Failure_ShouldExposeGenericMessage()
    {
        var controller =
            new StubController
            {
                ConnectFailure =
                    new InvalidDataException(
                        "Secret path and thumbprint")
            };
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                @"C:\Secret\client.json");

        await viewModel.ConnectAsync();

        Assert.Equal(
            "The runtime-host connection could not be started.",
            viewModel.FailureMessage);
        Assert.DoesNotContain(
            "Secret",
            viewModel.FailureMessage);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldUseController()
    {
        var controller =
            new StubController();
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                null);

        await viewModel.DisconnectAsync();

        Assert.Equal(
            1,
            controller.DisconnectCount);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public void Configure_SecondCall_ShouldThrow()
    {
        var viewModel =
            CreateConfiguredViewModel(
                new StubController(),
                null);

        Assert.Throws<InvalidOperationException>(
            () =>
                viewModel.Configure(
                    new StubController(),
                    new StubFilePicker(
                        null)));
    }

    [Fact]
    public void ApplySessionFailure_ShouldPreserveCategoryAndRedactDiagnostic()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionFailure(
            RuntimeHostClientFailureCategory.Authentication);

        Assert.Equal(
            RuntimeHostClientFailureCategory.Authentication,
            viewModel.LastFailureCategory);
        Assert.Equal(
            "Runtime-host authentication failed.",
            viewModel.FailureMessage);
    }

    [Theory]
    [InlineData(
        RuntimeHostClientSessionState.Connecting,
        false,
        true)]
    [InlineData(
        RuntimeHostClientSessionState.Disconnecting,
        false,
        false)]
    public void ApplySessionStatus_TransitionalState_ShouldControlActions(
        RuntimeHostClientSessionState state,
        bool canConnect,
        bool canDisconnect)
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionStatus(
            new RuntimeHostClientSessionStatus(
                state));

        Assert.Equal(
            canConnect,
            viewModel.CanConnect);
        Assert.Equal(
            canDisconnect,
            viewModel.CanDisconnect);
        Assert.False(
            viewModel.IsOperational);
        Assert.False(
            viewModel.IsStale);
    }

    private static RuntimeHostClientSessionStatus CreateStatus(
        RuntimeHostClientSessionState state) =>
        new(
            state,
            new RemoteRuntimeHostId(
                "runtime-host-01"),
            new RuntimeHostClientApiVersion(
                1,
                0));

    private static MainWindowViewModel CreateConfiguredViewModel(
        StubController controller,
        string? configurationFilePath)
    {
        var viewModel =
            new MainWindowViewModel();
        viewModel.Configure(
            controller,
            new StubFilePicker(
                configurationFilePath));

        return viewModel;
    }

    private sealed class StubFilePicker
        : IClientConfigurationFilePicker
    {
        private readonly string? configurationFilePath;

        public StubFilePicker(
            string? configurationFilePath)
        {
            this.configurationFilePath =
                configurationFilePath;
        }

        public string? PickConfigurationFile() =>
            configurationFilePath;
    }

    private sealed class StubController
        : IRuntimeHostClientSessionController
    {
        public Exception? ConnectFailure
        {
            get;
            init;
        }

        public string? ConfigurationFilePath
        {
            get;
            private set;
        }

        public int DisconnectCount
        {
            get;
            private set;
        }

        public Task ConnectAsync(
            string configurationFilePath,
            CancellationToken cancellationToken = default)
        {
            ConfigurationFilePath =
                configurationFilePath;

            return ConnectFailure is null
                ? Task.CompletedTask
                : Task.FromException(
                    ConnectFailure);
        }

        public Task DisconnectAsync()
        {
            DisconnectCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
