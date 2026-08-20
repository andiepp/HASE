using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class MainWindowEndpointRefreshTests
{
    [Fact]
    public async Task RefreshEndpointsAsync_ShouldDisableWhileActiveAndReproject()
    {
        using var context = new TestContext();

        Assert.False(context.ViewModel.RefreshEndpointsCommand.CanExecute());
        await context.ViewModel.StartAsync();
        Assert.True(context.ViewModel.RefreshEndpointsCommand.CanExecute());

        Task firstRefresh = context.ViewModel.RefreshEndpointsAsync();
        await context.Refresher.Entered;

        Assert.True(context.ViewModel.IsEndpointRefreshActive);
        Assert.False(context.ViewModel.RefreshEndpointsCommand.CanExecute());

        await context.ViewModel.RefreshEndpointsAsync();
        Assert.Equal(1, context.Refresher.CallCount);

        context.InventorySource.Snapshots =
            [
                new DesktopRuntimeEndpointSnapshot(
                    "endpoint-01",
                    "Endpoint 01",
                    "Ready",
                    Guid.NewGuid().ToString())
            ];
        context.Refresher.Release();
        await firstRefresh;

        Assert.False(context.ViewModel.IsEndpointRefreshActive);
        Assert.True(context.ViewModel.RefreshEndpointsCommand.CanExecute());
        Assert.Equal(1, context.Refresher.CallCount);
        Assert.True(context.InventorySource.CaptureCount >= 2);
        Assert.Single(context.ViewModel.Inventory.Endpoints);
        Assert.Equal(
            "endpoint-01",
            context.ViewModel.Inventory.Endpoints[0].EndpointId);

        await context.ViewModel.StopAsync();
        Assert.False(context.ViewModel.RefreshEndpointsCommand.CanExecute());
    }

    [Fact]
    public async Task StopAsync_ActiveRefresh_ShouldCancelBeforeBackendStop()
    {
        using var context = new TestContext();
        await context.ViewModel.StartAsync();

        Task refresh = context.ViewModel.RefreshEndpointsAsync();
        await context.Refresher.Entered;

        await context.ViewModel.StopAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => refresh);
        Assert.True(context.Refresher.CancellationObserved);
        Assert.Equal(1, context.Backend.StopCount);
        Assert.False(context.ViewModel.IsEndpointRefreshActive);
        Assert.False(context.ViewModel.RefreshEndpointsCommand.CanExecute());
    }

    [Fact]
    public async Task RefreshEndpointsAsync_BackendFailure_ShouldStillReenableCommand()
    {
        using var context = new TestContext(
            failRefresh: true);
        await context.ViewModel.StartAsync();

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.ViewModel.RefreshEndpointsAsync());

        Assert.Equal("refresh failed", failure.Message);
        Assert.False(context.ViewModel.IsEndpointRefreshActive);
        Assert.True(context.ViewModel.RefreshEndpointsCommand.CanExecute());
        Assert.True(context.InventorySource.CaptureCount >= 2);

        await context.ViewModel.StopAsync();
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(bool failRefresh = false)
        {
            Backend = new RecordingBackend();
            var runtimeHost = new DesktopRuntimeHost(Backend);
            var runtimeViewModel =
                new DesktopRuntimeHostViewModel(
                    runtimeHost,
                    new DesktopRuntimeHostShellInformation(
                        "composition",
                        "host",
                        "1.0",
                        "loopback",
                        "private"));
            InventorySource = new MutableInventorySource();
            var inventory = new RuntimeInventoryViewModel(InventorySource);
            var endpointDetails = new EndpointDetailsViewModel(inventory);
            Refresher = new RecordingRefresher(failRefresh);
            ViewModel =
                new MainWindowViewModel(
                    runtimeViewModel,
                    inventory,
                    endpointDetails,
                    new StubOperator(),
                    Refresher);
        }

        public RecordingBackend Backend { get; }

        public MutableInventorySource InventorySource { get; }

        public RecordingRefresher Refresher { get; }

        public MainWindowViewModel ViewModel { get; }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }

    private sealed class RecordingBackend : IDesktopRuntimeHostBackend
    {
        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRefresher
        : IDesktopRuntimeHostEndpointRefresher
    {
        private readonly bool failRefresh;
        private readonly TaskCompletionSource<bool> entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingRefresher(bool failRefresh)
        {
            this.failRefresh = failRefresh;
        }

        public int CallCount { get; private set; }

        public bool CancellationObserved { get; private set; }

        public Task Entered => entered.Task;

        public void Release()
        {
            release.TrySetResult(true);
        }

        public async Task RefreshEndpointsAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (failRefresh)
            {
                throw new InvalidOperationException("refresh failed");
            }

            entered.TrySetResult(true);

            try
            {
                await release.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class MutableInventorySource
        : IDesktopRuntimeHostInventorySource
    {
        public int CaptureCount { get; private set; }

        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Snapshots
        {
            get;
            set;
        } = [];

        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Capture()
        {
            CaptureCount++;
            return Snapshots;
        }
    }

    private sealed class StubOperator : IDesktopRuntimeHostOperator
    {
        public Task<Hase.Runtime.Northbound.RuntimeHostPropertyOperationResult>
            ReadPropertyAsync(
                Hase.Runtime.Northbound.RuntimeHostPropertyTarget target,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Hase.Runtime.Northbound.RuntimeHostPropertyOperationResult>
            WritePropertyAsync(
                Hase.Runtime.Northbound.RuntimeHostPropertyTarget target,
                object? requestedValue,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Hase.Runtime.Northbound.RuntimeHostCommandOperationResult>
            ExecuteCommandAsync(
                Hase.Runtime.Northbound.RuntimeHostCommandTarget target,
                object? argument,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
