using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeBooleanPropertyWriteTests
{
    [Fact]
    public async Task WriteAsync_ShouldCaptureTargetAndRequestedValueWithoutOptimisticUpdate()
    {
        RuntimeHostPropertyTarget target =
            CreateTarget(
                "08ba0f1c-b850-4a35-ab0d-373475aa1108");
        var property =
            CreatePropertyViewModel(
                target,
                currentValue: false);
        property.RequestedBooleanValue =
            true;

        var expected =
            RuntimeHostPropertyOperationResult.Successful(
                new PropertyValue(
                    true,
                    DateTimeOffset.Parse(
                        "2026-07-28T11:00:00+00:00")));
        var runtimeOperator =
            new RecordingOperator
            {
                WriteHandler =
                    (_, _, _) =>
                        Task.FromResult(
                            expected)
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        await viewModel.WriteBooleanPropertyAsync(
            property);

        Assert.Equal(
            1,
            runtimeOperator.WriteCount);
        Assert.Same(
            target,
            runtimeOperator.Target);
        Assert.Equal(
            true,
            runtimeOperator.RequestedValue);
        Assert.Equal(
            DesktopRuntimePropertyWriteState.Succeeded,
            property.WriteState);
        Assert.False(
            property.CurrentBooleanValue);
        Assert.True(
            property.RequestedBooleanValue);
    }

    [Fact]
    public async Task WriteAsync_WithNormalizedRejection_ShouldProjectRejectedState()
    {
        var property =
            CreatePropertyViewModel(
                CreateTarget(
                    "47c049de-fd8c-42bf-b68c-f052fd131586"),
                currentValue: false);
        var runtimeOperator =
            new RecordingOperator
            {
                WriteHandler =
                    (_, _, _) =>
                        Task.FromResult(
                            RuntimeHostPropertyOperationResult.Failed(
                                RuntimeHostPropertyOperationStatus
                                    .AttachmentNotCurrent,
                                "Attachment generation is stale."))
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        await viewModel.WriteBooleanPropertyAsync(
            property);

        Assert.Equal(
            DesktopRuntimePropertyWriteState.Rejected,
            property.WriteState);
        Assert.Equal(
            "Attachment generation is stale.",
            property.WriteMessage);
        Assert.False(
            property.RequestedBooleanValue);
    }

    [Fact]
    public async Task WriteAsync_WhenOperatorThrows_ShouldProjectFailedState()
    {
        var property =
            CreatePropertyViewModel(
                CreateTarget(
                    "fe0ad1c0-5ef1-4d91-baf4-6e1501102f2a"),
                currentValue: false);
        var runtimeOperator =
            new RecordingOperator
            {
                WriteHandler =
                    (_, _, _) =>
                        Task.FromException<
                            RuntimeHostPropertyOperationResult>(
                                new InvalidOperationException(
                                    "Operator unavailable."))
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        await viewModel.WriteBooleanPropertyAsync(
            property);

        Assert.Equal(
            DesktopRuntimePropertyWriteState.Failed,
            property.WriteState);
        Assert.Equal(
            "Operator unavailable.",
            property.WriteMessage);
        Assert.Equal(
            1,
            runtimeOperator.WriteCount);
    }

    [Fact]
    public async Task WriteAsync_WhenCancelled_ShouldProjectCancelledState()
    {
        var property =
            CreatePropertyViewModel(
                CreateTarget(
                    "9062632e-107d-4057-80e8-6b284cc59f2d"),
                currentValue: false);
        var runtimeOperator =
            new RecordingOperator
            {
                WriteHandler =
                    (_, _, cancellationToken) =>
                        Task.FromCanceled<
                            RuntimeHostPropertyOperationResult>(
                                cancellationToken)
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);
        using var cancellationSource =
            new CancellationTokenSource();
        cancellationSource.Cancel();

        await viewModel.WriteBooleanPropertyAsync(
            property,
            cancellationSource.Token);

        Assert.Equal(
            DesktopRuntimePropertyWriteState.Cancelled,
            property.WriteState);
        Assert.Equal(
            "Write cancelled.",
            property.WriteMessage);
    }

    [Fact]
    public async Task WriteAsync_WhileExecuting_ShouldNotStartOverlappingWrite()
    {
        var completion =
            new TaskCompletionSource<
                RuntimeHostPropertyOperationResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
        var property =
            CreatePropertyViewModel(
                CreateTarget(
                    "9cff744e-6d29-4110-b4ab-85f62d3c49fb"),
                currentValue: false);
        var runtimeOperator =
            new RecordingOperator
            {
                WriteHandler =
                    (_, _, _) =>
                        completion.Task
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        Task firstWrite =
            viewModel.WriteBooleanPropertyAsync(
                property);
        await viewModel.WriteBooleanPropertyAsync(
            property);

        Assert.Equal(
            1,
            runtimeOperator.WriteCount);
        Assert.True(
            property.IsWriteExecuting);

        completion.SetResult(
            RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.EndpointUnavailable));
        await firstWrite;

        Assert.Equal(
            DesktopRuntimePropertyWriteState.Failed,
            property.WriteState);
    }

    [Fact]
    public async Task WriteAsync_WhenGenerationChangesInFlight_ShouldKeepCapturedTarget()
    {
        RuntimeHostPropertyTarget originalTarget =
            CreateTarget(
                "12568667-6cbf-4018-b7d8-fe824987f06c");
        RuntimeHostPropertyTarget replacementTarget =
            CreateTarget(
                "10293285-a4b3-422f-86d4-81617337218f");
        var completion =
            new TaskCompletionSource<
                RuntimeHostPropertyOperationResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
        var property =
            CreatePropertyViewModel(
                originalTarget,
                currentValue: false);
        var runtimeOperator =
            new RecordingOperator
            {
                WriteHandler =
                    (_, _, _) =>
                        completion.Task
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        Task write =
            viewModel.WriteBooleanPropertyAsync(
                property);

        property.Update(
            CreatePropertySnapshot(
                replacementTarget,
                currentValue: false));

        Assert.Same(
            originalTarget,
            runtimeOperator.Target);
        Assert.Same(
            replacementTarget,
            property.Target);

        completion.SetResult(
            RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.AttachmentNotCurrent));
        await write;

        Assert.Equal(
            DesktopRuntimePropertyWriteState.Rejected,
            property.WriteState);
        Assert.False(
            property.RequestedBooleanValue);
    }

    [Fact]
    public async Task WriteAsync_WhenEndpointIsNotReady_ShouldNotCallOperator()
    {
        DesktopRuntimePropertySnapshot snapshot =
            CreatePropertySnapshot(
                CreateTarget(
                    "20d36f7c-ff20-45ba-b0b0-0c23d45ad931"),
                currentValue: false)
            with
            {
                IsEndpointReady =
                    false
            };
        var property =
            new DesktopRuntimePropertyViewModel(
                snapshot);
        var runtimeOperator =
            new RecordingOperator();
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        await viewModel.WriteBooleanPropertyAsync(
            property);

        Assert.Equal(
            0,
            runtimeOperator.WriteCount);
        Assert.Equal(
            DesktopRuntimePropertyWriteState.Ready,
            property.WriteState);
    }

    private static DesktopRuntimePropertyViewModel CreatePropertyViewModel(
        RuntimeHostPropertyTarget target,
        bool currentValue) =>
        new(
            CreatePropertySnapshot(
                target,
                currentValue));

    private static DesktopRuntimePropertySnapshot CreatePropertySnapshot(
        RuntimeHostPropertyTarget target,
        bool currentValue) =>
        new(
            target,
            target.PropertyId.Value,
            "Built-in LED state",
            "Led.State",
            "ReadWrite",
            currentValue.ToString(),
            "Good",
            "2026-07-28T11:00:00.0000000+00:00",
            IsKnown: true,
            DesktopRuntimePropertyDataKind.Boolean,
            CanRead: true,
            CanWrite: true,
            BooleanValue: currentValue,
            IsEndpointReady: true);

    private static RuntimeHostPropertyTarget CreateTarget(
        string generation) =>
        new(
            new EndpointId("endpoint-1"),
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse(
                    generation)),
            new InstrumentId("instrument-1"),
            new PropertyId("property-1"));

    private static MainWindowViewModel CreateMainWindowViewModel(
        IDesktopRuntimeHostOperator runtimeOperator)
    {
        var runtimeHost =
            new DesktopRuntimeHost(
                new StubBackend());
        var runtimeViewModel =
            new DesktopRuntimeHostViewModel(
                runtimeHost,
                new DesktopRuntimeHostShellInformation(
                    "composition",
                    "host",
                    "1.0",
                    "loopback",
                    "private"));
        runtimeHost.StartAsync()
            .GetAwaiter()
            .GetResult();
        var inventoryViewModel =
            new RuntimeInventoryViewModel(
                EmptyInventorySource.Instance);
        var endpointDetailsViewModel =
            new EndpointDetailsViewModel(
                inventoryViewModel);

        return new MainWindowViewModel(
            runtimeViewModel,
            inventoryViewModel,
            endpointDetailsViewModel,
            runtimeOperator);
    }

    private sealed class RecordingOperator
        : IDesktopRuntimeHostOperator
    {
        public Task<RuntimeHostPropertyOperationResult> ReadPropertyAsync(
            RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Func<
            RuntimeHostPropertyTarget,
            object?,
            CancellationToken,
            Task<RuntimeHostPropertyOperationResult>>? WriteHandler
        {
            get;
            init;
        }

        public int WriteCount
        {
            get;
            private set;
        }

        public RuntimeHostPropertyTarget? Target
        {
            get;
            private set;
        }

        public object? RequestedValue
        {
            get;
            private set;
        }

        public Task<RuntimeHostPropertyOperationResult> WritePropertyAsync(
            RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            Target =
                target;
            RequestedValue =
                requestedValue;

            return WriteHandler?.Invoke(
                    target,
                    requestedValue,
                    cancellationToken)
                ?? Task.FromResult(
                    RuntimeHostPropertyOperationResult.Failed(
                        RuntimeHostPropertyOperationStatus
                            .EndpointUnavailable));
        }

        public Task<RuntimeHostCommandOperationResult> ExecuteCommandAsync(
            RuntimeHostCommandTarget target,
            object? argument,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubBackend
        : IDesktopRuntimeHostBackend
    {
        public Task StartAsync(
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task StopAsync(
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class EmptyInventorySource
        : IDesktopRuntimeHostInventorySource
    {
        public static EmptyInventorySource Instance
        {
            get;
        } =
            new();

        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Capture() =>
            [];
    }
}
