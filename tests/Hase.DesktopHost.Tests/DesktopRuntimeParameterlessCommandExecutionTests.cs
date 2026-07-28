using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.ViewModels;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeParameterlessCommandExecutionTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCaptureTargetPassNullAndProjectReturnValue()
    {
        RuntimeHostCommandTarget target =
            CreateTarget(
                "087f2821-f93e-4630-aa76-48d3ccbb62a6");
        var command =
            CreateCommandViewModel(
                target);
        var runtimeOperator =
            new RecordingOperator
            {
                ExecuteHandler =
                    (_, _, _) =>
                        Task.FromResult(
                            RuntimeHostCommandOperationResult.Successful(
                                true))
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        await viewModel.ExecuteParameterlessCommandAsync(
            command);

        Assert.Equal(
            1,
            runtimeOperator.ExecuteCount);
        Assert.Same(
            target,
            runtimeOperator.Target);
        Assert.Null(
            runtimeOperator.Argument);
        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Succeeded,
            command.ExecutionState);
        Assert.True(
            command.HasReturnValue);
        Assert.Equal(
            "True",
            command.ReturnValue);
        DesktopRuntimeOperatorActivityEntry activity =
            Assert.Single(
                viewModel.Activity.Entries);
        Assert.Equal(
            DesktopRuntimeOperatorActivityKind
                .ParameterlessCommandExecution,
            activity.Kind);
        Assert.Equal(
            target.EndpointId.Value,
            activity.EndpointId);
        Assert.Equal(
            target.AttachmentGeneration.ToString(),
            activity.AttachmentGeneration);
        Assert.Equal(
            target.InstrumentId.Value,
            activity.InstrumentId);
        Assert.Equal(
            command.Path,
            activity.OperationPath);
        Assert.Equal(
            "None",
            activity.InputSummary);
        Assert.Equal(
            DesktopRuntimeOperatorActivityOutcome.Succeeded,
            activity.Outcome);
        Assert.Equal(
            "No readable Properties required refresh.",
            activity.Reconciliation);
    }

    [Fact]
    public async Task ExecuteAsync_WithNormalizedRejection_ShouldProjectRejectedState()
    {
        var command =
            CreateCommandViewModel(
                CreateTarget(
                    "9e2e33c2-ccba-480f-bf2c-11302751758e"));
        var runtimeOperator =
            new RecordingOperator
            {
                ExecuteHandler =
                    (_, _, _) =>
                        Task.FromResult(
                            RuntimeHostCommandOperationResult.Failed(
                                RuntimeHostCommandOperationStatus
                                    .AttachmentNotCurrent,
                                "Attachment generation is stale."))
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        await viewModel.ExecuteParameterlessCommandAsync(
            command);

        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Rejected,
            command.ExecutionState);
        Assert.Equal(
            "Attachment generation is stale.",
            command.ExecutionMessage);
        DesktopRuntimeOperatorActivityEntry activity =
            Assert.Single(
                viewModel.Activity.Entries);
        Assert.Equal(
            DesktopRuntimeOperatorActivityOutcome.Rejected,
            activity.Outcome);
        Assert.Equal(
            "Attachment generation is stale.",
            activity.Diagnostic);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperatorThrows_ShouldProjectFailedState()
    {
        var command =
            CreateCommandViewModel(
                CreateTarget(
                    "5025c8ba-9a48-49ba-a1a5-a10c739804dd"));
        var runtimeOperator =
            new RecordingOperator
            {
                ExecuteHandler =
                    (_, _, _) =>
                        Task.FromException<
                            RuntimeHostCommandOperationResult>(
                                new InvalidOperationException(
                                    "Operator unavailable."))
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        await viewModel.ExecuteParameterlessCommandAsync(
            command);

        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Failed,
            command.ExecutionState);
        Assert.Equal(
            "Operator unavailable.",
            command.ExecutionMessage);
        Assert.Equal(
            1,
            runtimeOperator.ExecuteCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldProjectCancelledState()
    {
        var command =
            CreateCommandViewModel(
                CreateTarget(
                    "4f6ed748-70dc-462c-a322-a85c5ab51e6c"));
        var runtimeOperator =
            new RecordingOperator
            {
                ExecuteHandler =
                    (_, _, cancellationToken) =>
                        Task.FromCanceled<
                            RuntimeHostCommandOperationResult>(
                                cancellationToken)
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);
        using var cancellationSource =
            new CancellationTokenSource();
        cancellationSource.Cancel();

        await viewModel.ExecuteParameterlessCommandAsync(
            command,
            cancellationSource.Token);

        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Cancelled,
            command.ExecutionState);
        Assert.Equal(
            "Command cancelled.",
            command.ExecutionMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhileExecuting_ShouldNotStartOverlappingExecution()
    {
        var completion =
            new TaskCompletionSource<
                RuntimeHostCommandOperationResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
        var command =
            CreateCommandViewModel(
                CreateTarget(
                    "c6437ab4-7e2a-4b90-8bd4-e5c22264073e"));
        var runtimeOperator =
            new RecordingOperator
            {
                ExecuteHandler =
                    (_, _, _) =>
                        completion.Task
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        Task firstExecution =
            viewModel.ExecuteParameterlessCommandAsync(
                command);
        await viewModel.ExecuteParameterlessCommandAsync(
            command);

        Assert.Equal(
            1,
            runtimeOperator.ExecuteCount);
        Assert.True(
            command.IsExecuting);

        completion.SetResult(
            RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.EndpointUnavailable));
        await firstExecution;

        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Failed,
            command.ExecutionState);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGenerationChangesInFlight_ShouldKeepCapturedTarget()
    {
        RuntimeHostCommandTarget originalTarget =
            CreateTarget(
                "21cfca79-7a7f-4609-84b0-90eae2b10955");
        RuntimeHostCommandTarget replacementTarget =
            CreateTarget(
                "f0cfc45e-0851-4f0a-a8da-79e37401db22");
        var completion =
            new TaskCompletionSource<
                RuntimeHostCommandOperationResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
        var command =
            CreateCommandViewModel(
                originalTarget);
        var runtimeOperator =
            new RecordingOperator
            {
                ExecuteHandler =
                    (_, _, _) =>
                        completion.Task
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        Task execution =
            viewModel.ExecuteParameterlessCommandAsync(
                command);

        command.Update(
            CreateCommandSnapshot(
                replacementTarget,
                isEndpointReady: true));

        Assert.Same(
            originalTarget,
            runtimeOperator.Target);
        Assert.Same(
            replacementTarget,
            command.Target);

        completion.SetResult(
            RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.AttachmentNotCurrent));
        await execution;

        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Rejected,
            command.ExecutionState);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEndpointIsNotReady_ShouldNotCallOperator()
    {
        var command =
            new DesktopRuntimeCommandViewModel(
                CreateCommandSnapshot(
                    CreateTarget(
                        "ca868452-e2ee-4896-81a3-e50325c33e3c"),
                    isEndpointReady: false));
        var runtimeOperator =
            new RecordingOperator();
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator);

        await viewModel.ExecuteParameterlessCommandAsync(
            command);

        Assert.Equal(
            0,
            runtimeOperator.ExecuteCount);
        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Ready,
            command.ExecutionState);
        Assert.Empty(
            viewModel.Activity.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_AfterSuccess_ShouldAuthoritativelyRefreshReadableProperties()
    {
        RuntimeHostCommandTarget commandTarget =
            CreateTarget(
                "80ac60d8-bcb0-4822-909a-a657033431d7");
        RuntimeHostPropertyTarget propertyTarget =
            CreatePropertyTarget(
                commandTarget);
        var inventorySource =
            new MutableInventorySource
            {
                Snapshots =
                    [
                        CreateEndpointSnapshot(
                            commandTarget,
                            propertyTarget,
                            currentValue: true)
                    ]
            };
        var runtimeOperator =
            new RecordingOperator
            {
                ExecuteHandler =
                    (_, _, _) =>
                        Task.FromResult(
                            RuntimeHostCommandOperationResult.Successful(
                                false)),
                ReadHandler =
                    (_, _) =>
                    {
                        inventorySource.Snapshots =
                        [
                            CreateEndpointSnapshot(
                                commandTarget,
                                propertyTarget,
                                currentValue: false)
                        ];

                        return Task.FromResult(
                            RuntimeHostPropertyOperationResult.Successful(
                                new Hase.Core.Domain.Properties.PropertyValue(
                                    false,
                                    DateTimeOffset.Parse(
                                        "2026-07-28T11:30:00+00:00"))));
                    }
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator,
                inventorySource);
        DesktopRuntimeCommandViewModel command =
            viewModel.Inventory.Endpoints[0].Instruments[0].Commands[0];

        await viewModel.ExecuteParameterlessCommandAsync(
            command);

        Assert.Equal(
            1,
            runtimeOperator.ReadCount);
        Assert.Same(
            propertyTarget,
            runtimeOperator.ReadTargets[0]);
        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Succeeded,
            command.ExecutionState);
        Assert.Contains(
            "authoritatively refreshed 1 Property",
            command.ExecutionMessage);
        Assert.False(
            viewModel.Inventory.Endpoints[0]
                .Instruments[0]
                .Properties[0]
                .CurrentBooleanValue);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPropertyRefreshFails_ShouldKeepSuccessAndReportWarning()
    {
        RuntimeHostCommandTarget commandTarget =
            CreateTarget(
                "eef28aba-97a8-4ec3-9da2-dad8be41bd97");
        RuntimeHostPropertyTarget propertyTarget =
            CreatePropertyTarget(
                commandTarget);
        var inventorySource =
            new MutableInventorySource
            {
                Snapshots =
                    [
                        CreateEndpointSnapshot(
                            commandTarget,
                            propertyTarget,
                            currentValue: true)
                    ]
            };
        var runtimeOperator =
            new RecordingOperator
            {
                ExecuteHandler =
                    (_, _, _) =>
                        Task.FromResult(
                            RuntimeHostCommandOperationResult.Successful(
                                false)),
                ReadHandler =
                    (_, _) =>
                        Task.FromResult(
                            RuntimeHostPropertyOperationResult.Failed(
                                RuntimeHostPropertyOperationStatus
                                    .EndpointUnavailable))
            };
        using MainWindowViewModel viewModel =
            CreateMainWindowViewModel(
                runtimeOperator,
                inventorySource);
        DesktopRuntimeCommandViewModel command =
            viewModel.Inventory.Endpoints[0].Instruments[0].Commands[0];

        await viewModel.ExecuteParameterlessCommandAsync(
            command);

        Assert.Equal(
            DesktopRuntimeCommandExecutionState.Succeeded,
            command.ExecutionState);
        Assert.Contains(
            "Property reconciliation warning",
            command.ExecutionMessage);
        Assert.Contains(
            "EndpointUnavailable",
            command.ExecutionMessage);
    }

    private static DesktopRuntimeCommandViewModel CreateCommandViewModel(
        RuntimeHostCommandTarget target) =>
        new(
            CreateCommandSnapshot(
                target,
                isEndpointReady: true));

    private static DesktopRuntimeCommandSnapshot CreateCommandSnapshot(
        RuntimeHostCommandTarget target,
        bool isEndpointReady) =>
        new(
            target,
            target.CommandPath.ToString(),
            "Toggle status LED",
            "Toggles the endpoint status LED.",
            isEndpointReady);

    private static RuntimeHostCommandTarget CreateTarget(
        string generation) =>
        new(
            new EndpointId("endpoint-1"),
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse(
                    generation)),
            new InstrumentId("instrument-1"),
            new DescriptorPath(
                "Controller",
                "Toggle"));

    private static RuntimeHostPropertyTarget CreatePropertyTarget(
        RuntimeHostCommandTarget commandTarget) =>
        new(
            commandTarget.EndpointId,
            commandTarget.AttachmentGeneration,
            commandTarget.InstrumentId,
            new PropertyId("property-1"));

    private static DesktopRuntimeEndpointSnapshot CreateEndpointSnapshot(
        RuntimeHostCommandTarget commandTarget,
        RuntimeHostPropertyTarget propertyTarget,
        bool currentValue) =>
        new(
            commandTarget.EndpointId.Value,
            "Endpoint",
            "Ready",
            commandTarget.AttachmentGeneration.ToString())
        {
            Instruments =
                [
                    new DesktopRuntimeInstrumentSnapshot(
                        commandTarget.InstrumentId.Value,
                        "Controller",
                        "Controller",
                        "HASE",
                        "Endpoint",
                        null,
                        null,
                        null,
                        null)
                    {
                        Properties =
                            [
                                new DesktopRuntimePropertySnapshot(
                                    propertyTarget,
                                    propertyTarget.PropertyId.Value,
                                    "Status LED Enabled",
                                    "Controller.StatusLedEnabled",
                                    "ReadWrite",
                                    currentValue.ToString(),
                                    "Good",
                                    "2026-07-28T11:30:00.0000000+00:00",
                                    IsKnown: true,
                                    DesktopRuntimePropertyDataKind.Boolean,
                                    CanRead: true,
                                    CanWrite: true,
                                    BooleanValue: currentValue,
                                    IsEndpointReady: true)
                            ],
                        Commands =
                            [
                                CreateCommandSnapshot(
                                    commandTarget,
                                    isEndpointReady: true)
                            ]
                    }
                ]
        };

    private static MainWindowViewModel CreateMainWindowViewModel(
        IDesktopRuntimeHostOperator runtimeOperator,
        IDesktopRuntimeHostInventorySource? inventorySource = null)
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
                inventorySource
                ?? EmptyInventorySource.Instance);
        var endpointDetailsViewModel =
            new EndpointDetailsViewModel(
                inventoryViewModel);

        var viewModel =
            new MainWindowViewModel(
            runtimeViewModel,
            inventoryViewModel,
            endpointDetailsViewModel,
            runtimeOperator);

        viewModel.RefreshInventory();
        viewModel.RefreshInventory();

        return viewModel;
    }

    private sealed class RecordingOperator
        : IDesktopRuntimeHostOperator
    {
        public Func<
            RuntimeHostPropertyTarget,
            CancellationToken,
            Task<RuntimeHostPropertyOperationResult>>? ReadHandler
        {
            get;
            init;
        }

        public int ReadCount
        {
            get;
            private set;
        }

        public List<RuntimeHostPropertyTarget> ReadTargets
        {
            get;
        } =
            [];

        public Func<
            RuntimeHostCommandTarget,
            object?,
            CancellationToken,
            Task<RuntimeHostCommandOperationResult>>? ExecuteHandler
        {
            get;
            init;
        }

        public int ExecuteCount
        {
            get;
            private set;
        }

        public RuntimeHostCommandTarget? Target
        {
            get;
            private set;
        }

        public object? Argument
        {
            get;
            private set;
        }

        public Task<RuntimeHostPropertyOperationResult> ReadPropertyAsync(
            RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            ReadTargets.Add(
                target);

            return ReadHandler?.Invoke(
                    target,
                    cancellationToken)
                ?? Task.FromResult(
                    RuntimeHostPropertyOperationResult.Failed(
                        RuntimeHostPropertyOperationStatus
                            .EndpointUnavailable));
        }

        public Task<RuntimeHostPropertyOperationResult> WritePropertyAsync(
            RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeHostCommandOperationResult> ExecuteCommandAsync(
            RuntimeHostCommandTarget target,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            Target =
                target;
            Argument =
                argument;

            return ExecuteHandler?.Invoke(
                    target,
                    argument,
                    cancellationToken)
                ?? Task.FromResult(
                    RuntimeHostCommandOperationResult.Failed(
                        RuntimeHostCommandOperationStatus
                            .EndpointUnavailable));
        }
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

    private sealed class MutableInventorySource
        : IDesktopRuntimeHostInventorySource
    {
        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Snapshots
        {
            get;
            set;
        } =
            [];

        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Capture() =>
            Snapshots;
    }
}
