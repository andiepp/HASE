using Prism.Commands;
using System.Globalization;
using System.Windows.Threading;
using Hase.Core.Domain.Data;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class MainWindowViewModel : IDisposable
{
    private readonly DesktopRuntimeHostViewModel runtimeHostViewModel;
    private readonly IDesktopRuntimeHostOperator runtimeHostOperator;
    private readonly IDesktopRuntimeHostEndpointRefresher endpointRefresher;
    private readonly IDesktopRuntimeHostEventSource? eventSource;
    private readonly Dispatcher dispatcher;
    private readonly object endpointRefreshSyncRoot = new();
    private CancellationTokenSource? eventObservationCancellation;
    private Task? eventObservationTask;
    private CancellationTokenSource? endpointRefreshCancellation;
    private Task? endpointRefreshTask;
    private bool endpointRefreshActive;
    private bool disposed;

    public MainWindowViewModel(
        DesktopRuntimeHostViewModel runtimeHostViewModel,
        RuntimeInventoryViewModel inventoryViewModel,
        EndpointDetailsViewModel endpointDetailsViewModel,
        IDesktopRuntimeHostOperator runtimeHostOperator,
        IDesktopRuntimeHostEndpointRefresher endpointRefresher,
        IDesktopRuntimeHostEventSource? eventSource = null,
        RuntimeDiagnosticsViewModel? diagnosticsViewModel = null)
    {
        this.runtimeHostViewModel =
            runtimeHostViewModel
            ?? throw new ArgumentNullException(
                nameof(runtimeHostViewModel));
        Inventory =
            inventoryViewModel
            ?? throw new ArgumentNullException(
                nameof(inventoryViewModel));
        EndpointDetails =
            endpointDetailsViewModel
            ?? throw new ArgumentNullException(
                nameof(endpointDetailsViewModel));
        this.runtimeHostOperator =
            runtimeHostOperator
            ?? throw new ArgumentNullException(
                nameof(runtimeHostOperator));
        this.endpointRefresher =
            endpointRefresher
            ?? throw new ArgumentNullException(
                nameof(endpointRefresher));
        Activity =
            new OperatorActivityViewModel();
        EndpointEvents =
            new EndpointEventHistoryViewModel();
        Diagnostics =
            diagnosticsViewModel
            ?? new RuntimeDiagnosticsViewModel();
        this.eventSource =
            eventSource;
        dispatcher =
            Dispatcher.CurrentDispatcher;

        WritePropertyCommand =
            new DelegateCommand<DesktopRuntimePropertyViewModel>(
                ExecuteWriteProperty,
                property =>
                    property?.CanWriteRequestedValue
                    == true
                    && RuntimeHost.Status
                        == DesktopRuntimeHostStatus.Running);
        ExecuteParameterlessCommand =
            new DelegateCommand<DesktopRuntimeCommandViewModel>(
                ExecuteCommand,
                command =>
                    command?.CanExecute
                    == true
                    && RuntimeHost.Status
                        == DesktopRuntimeHostStatus.Running);
        RefreshEndpointsCommand =
            new DelegateCommand(
                ExecuteRefreshEndpoints,
                CanRefreshEndpoints);
    }

    public string ApplicationTitle =>
        "HASE Runtime Host";

    public DesktopRuntimeHostViewModel RuntimeHost =>
        runtimeHostViewModel;

    public RuntimeInventoryViewModel Inventory
    {
        get;
    }

    public EndpointDetailsViewModel EndpointDetails
    {
        get;
    }

    public OperatorActivityViewModel Activity
    {
        get;
    }

    public EndpointEventHistoryViewModel EndpointEvents
    {
        get;
    }

    public RuntimeDiagnosticsViewModel Diagnostics
    {
        get;
    }

    public DelegateCommand<DesktopRuntimePropertyViewModel>
        WritePropertyCommand
    {
        get;
    }

    public DelegateCommand<DesktopRuntimePropertyViewModel>
        WriteBooleanPropertyCommand =>
            WritePropertyCommand;

    public DelegateCommand<DesktopRuntimeCommandViewModel>
        ExecuteParameterlessCommand
    {
        get;
    }

    public DelegateCommand RefreshEndpointsCommand
    {
        get;
    }

    public bool IsEndpointRefreshActive
    {
        get
        {
            lock (endpointRefreshSyncRoot)
            {
                return endpointRefreshActive;
            }
        }
    }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        await runtimeHostViewModel.StartAsync(
            cancellationToken);

        RefreshInventory();

        if (eventSource is not null)
        {
            eventObservationCancellation =
                new CancellationTokenSource();
            eventObservationTask =
                ObserveEventsAsync(
                    eventObservationCancellation.Token);
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await CancelEndpointRefreshAsync()
                .ConfigureAwait(false);

            if (eventObservationCancellation is not null)
            {
                await eventObservationCancellation.CancelAsync()
                    .ConfigureAwait(false);

                if (eventObservationTask is not null)
                {
                    await eventObservationTask
                        .ConfigureAwait(false);
                }

                eventObservationCancellation.Dispose();
                eventObservationCancellation =
                    null;
                eventObservationTask =
                    null;
            }
        }
        finally
        {
            await runtimeHostViewModel.StopAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task RefreshEndpointsAsync(
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource refreshCancellation;
        Task refreshTask;

        lock (endpointRefreshSyncRoot)
        {
            if (disposed
                || endpointRefreshActive
                || RuntimeHost.Status
                    != DesktopRuntimeHostStatus.Running)
            {
                return;
            }

            endpointRefreshActive = true;
            refreshCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            refreshTask =
                RunEndpointRefreshAsync(
                    refreshCancellation.Token);
            endpointRefreshCancellation = refreshCancellation;
            endpointRefreshTask = refreshTask;
        }

        RefreshEndpointsCommand.RaiseCanExecuteChanged();

        try
        {
            await refreshTask;
        }
        finally
        {
            lock (endpointRefreshSyncRoot)
            {
                if (ReferenceEquals(
                        endpointRefreshCancellation,
                        refreshCancellation))
                {
                    endpointRefreshCancellation = null;
                    endpointRefreshTask = null;
                    endpointRefreshActive = false;
                }
            }

            refreshCancellation.Dispose();
            RefreshInventory();
        }
    }

    public void RefreshInventory()
    {
        Inventory.Refresh();
        Diagnostics.Refresh();
        WritePropertyCommand.RaiseCanExecuteChanged();
        ExecuteParameterlessCommand.RaiseCanExecuteChanged();
        RefreshEndpointsCommand.RaiseCanExecuteChanged();
    }

    public async Task ExecuteParameterlessCommandAsync(
        DesktopRuntimeCommandViewModel command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        object? argument =
            command.InputResult.IsSuccess
                ? command.InputResult.Value
                : null;
        Hase.Runtime.Northbound.RuntimeHostCommandTarget? target =
            RuntimeHost.Status
                == DesktopRuntimeHostStatus.Running
                ? command.TryBeginExecution()
                : null;

        ExecuteParameterlessCommand.RaiseCanExecuteChanged();

        if (target is null)
        {
            return;
        }

        string operationPath =
            command.Path;
        string inputSummary =
            FormatCommandArgument(
                argument);
        string reconciliation =
            string.Empty;

        try
        {
            Hase.Runtime.Northbound.RuntimeHostCommandOperationResult result =
                await runtimeHostOperator.ExecuteCommandAsync(
                    target,
                    argument,
                    cancellationToken);

            command.CompleteExecution(
                result);

            if (result.IsSuccess)
            {
                reconciliation =
                    await ReconcileReadablePropertiesAsync(
                        command,
                        target,
                        cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            command.CancelExecution();
        }
        catch (Exception exception)
        {
            command.FailExecution(
                exception);
        }
        finally
        {
            Activity.Record(
                DesktopRuntimeOperatorActivityKind
                    .ParameterlessCommandExecution,
                target.EndpointId.Value,
                target.AttachmentGeneration.ToString(),
                target.InstrumentId.Value,
                operationPath,
                inputSummary,
                GetActivityOutcome(
                    command.ExecutionState),
                command.ExecutionState
                    == DesktopRuntimeCommandExecutionState.Succeeded
                        ? string.Empty
                        : command.ExecutionMessage,
                reconciliation);
            ExecuteParameterlessCommand.RaiseCanExecuteChanged();
        }
    }

    public async Task WritePropertyAsync(
        DesktopRuntimePropertyViewModel property,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            property);

        DesktopRuntimePropertyWriteRequest? request =
            RuntimeHost.Status
                == DesktopRuntimeHostStatus.Running
                ? property.TryBeginWrite()
                : null;

        WritePropertyCommand.RaiseCanExecuteChanged();

        if (request is null)
        {
            return;
        }

        string operationPath =
            property.Path;

        try
        {
            Hase.Runtime.Northbound.RuntimeHostPropertyOperationResult result =
                await runtimeHostOperator.WritePropertyAsync(
                    request.Target,
                    request.RequestedValue,
                    cancellationToken);

            property.CompleteWrite(
                result);
        }
        catch (OperationCanceledException)
        {
            property.CancelWrite();
        }
        catch (Exception exception)
        {
            property.FailWrite(
                exception);
        }
        finally
        {
            Activity.Record(
                DesktopRuntimeOperatorActivityKind.PropertyWrite,
                request.Target.EndpointId.Value,
                request.Target.AttachmentGeneration.ToString(),
                request.Target.InstrumentId.Value,
                operationPath,
                request.InputSummary,
                GetActivityOutcome(
                    property.WriteState),
                property.WriteState
                    == DesktopRuntimePropertyWriteState.Succeeded
                        ? string.Empty
                        : property.WriteMessage);
            WritePropertyCommand.RaiseCanExecuteChanged();
        }
    }

    public Task WriteBooleanPropertyAsync(
        DesktopRuntimePropertyViewModel property,
        CancellationToken cancellationToken = default)
    {
        return WritePropertyAsync(
            property,
            cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        lock (endpointRefreshSyncRoot)
        {
            endpointRefreshCancellation?.Cancel();
        }

        EndpointDetails.Dispose();
        runtimeHostViewModel.Dispose();
    }

    private async void ExecuteWriteProperty(
        DesktopRuntimePropertyViewModel property)
    {
        await WritePropertyAsync(
            property,
            CancellationToken.None);
    }

    private async void ExecuteRefreshEndpoints()
    {
        try
        {
            await RefreshEndpointsAsync(
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // The backend publishes sanitized per-endpoint outcomes. A
            // lifecycle race must not terminate the WPF dispatcher.
        }
    }

    private bool CanRefreshEndpoints()
    {
        lock (endpointRefreshSyncRoot)
        {
            return !disposed
                && !endpointRefreshActive
                && RuntimeHost.Status
                    == DesktopRuntimeHostStatus.Running;
        }
    }

    private async Task RunEndpointRefreshAsync(
        CancellationToken cancellationToken)
    {
        await endpointRefresher.RefreshEndpointsAsync(
            cancellationToken);
    }

    private async Task CancelEndpointRefreshAsync()
    {
        CancellationTokenSource? cancellation;
        Task? refreshTask;

        lock (endpointRefreshSyncRoot)
        {
            cancellation = endpointRefreshCancellation;
            refreshTask = endpointRefreshTask;
        }

        if (cancellation is not null)
        {
            await cancellation.CancelAsync()
                .ConfigureAwait(false);
        }

        if (refreshTask is not null)
        {
            try
            {
                await refreshTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task ObserveEventsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (
                DesktopRuntimeEventOccurrence occurrence
                in eventSource!.ObserveEventsAsync(
                    cancellationToken)
                    .ConfigureAwait(false))
            {
                await dispatcher.InvokeAsync(
                    () =>
                        EndpointEvents.Record(
                            occurrence),
                    DispatcherPriority.DataBind,
                    cancellationToken)
                    .Task
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // Observation is read-only. A terminated subscription must not
            // terminate the WPF dispatcher or prevent orderly host shutdown.
        }
    }

    private async void ExecuteCommand(
        DesktopRuntimeCommandViewModel command)
    {
        await ExecuteParameterlessCommandAsync(
            command,
            CancellationToken.None);
    }

    private async Task<string> ReconcileReadablePropertiesAsync(
        DesktopRuntimeCommandViewModel command,
        Hase.Runtime.Northbound.RuntimeHostCommandTarget commandTarget,
        CancellationToken cancellationToken)
    {
        DesktopRuntimePropertyViewModel[] readableProperties =
            Inventory.Endpoints
                .Where(
                    endpoint =>
                        string.Equals(
                            endpoint.EndpointId,
                            commandTarget.EndpointId.Value,
                            StringComparison.Ordinal)
                        && string.Equals(
                            endpoint.AttachmentGeneration,
                            commandTarget.AttachmentGeneration.ToString(),
                            StringComparison.Ordinal))
                .SelectMany(
                    endpoint =>
                        endpoint.Instruments)
                .Where(
                    instrument =>
                        string.Equals(
                            instrument.InstrumentId,
                            commandTarget.InstrumentId.Value,
                            StringComparison.Ordinal))
                .SelectMany(
                    instrument =>
                        instrument.Properties)
                .Where(
                    property =>
                        property.CanRead
                        && property.Target.EndpointId
                            == commandTarget.EndpointId
                        && property.Target.AttachmentGeneration
                            == commandTarget.AttachmentGeneration
                        && property.Target.InstrumentId
                            == commandTarget.InstrumentId)
                .ToArray();

        var warnings =
            new List<string>();
        int refreshedPropertyCount =
            0;

        foreach (
            DesktopRuntimePropertyViewModel property
            in readableProperties)
        {
            try
            {
                Hase.Runtime.Northbound.RuntimeHostPropertyOperationResult
                    readResult =
                        await runtimeHostOperator.ReadPropertyAsync(
                            property.Target,
                            cancellationToken);

                if (readResult.IsSuccess)
                {
                    refreshedPropertyCount++;
                }
                else
                {
                    warnings.Add(
                        $"{property.Path}: {readResult.Status}");
                }
            }
            catch (OperationCanceledException)
            {
                warnings.Add(
                    "authoritative Property refresh was cancelled");
                break;
            }
            catch (Exception exception)
            {
                warnings.Add(
                    $"{property.Path}: "
                    + (
                        string.IsNullOrWhiteSpace(exception.Message)
                            ? "authoritative read failed"
                            : exception.Message));
            }
        }

        RefreshInventory();

        if (warnings.Count == 0)
        {
            command.CompletePropertyReconciliation(
                refreshedPropertyCount);
            return refreshedPropertyCount == 0
                ? "No readable Properties required refresh."
                : $"Authoritatively refreshed {refreshedPropertyCount} "
                    + $"{(refreshedPropertyCount == 1 ? "Property" : "Properties")}.";
        }

        string warning =
            string.Join(
                "; ",
                warnings);
        command.ReportPropertyReconciliationWarning(
            warning);
        return "Warning: "
            + warning;
    }

    private static string FormatCommandArgument(
        object? argument)
    {
        if (argument is null)
        {
            return "None";
        }

        if (argument is ByteArrayValue byteArray)
        {
            return string.Join(
                " ",
                byteArray
                    .ToArray()
                    .Select(
                        item =>
                            item.ToString(
                                "X2",
                                CultureInfo.InvariantCulture)));
        }

        return argument is IFormattable formattable
            ? formattable.ToString(
                format: null,
                CultureInfo.InvariantCulture)
                ?? string.Empty
            : argument.ToString()
                ?? string.Empty;
    }

    private static DesktopRuntimeOperatorActivityOutcome GetActivityOutcome(
        DesktopRuntimeCommandExecutionState state) =>
        state switch
        {
            DesktopRuntimeCommandExecutionState.Succeeded =>
                DesktopRuntimeOperatorActivityOutcome.Succeeded,
            DesktopRuntimeCommandExecutionState.Rejected =>
                DesktopRuntimeOperatorActivityOutcome.Rejected,
            DesktopRuntimeCommandExecutionState.Cancelled =>
                DesktopRuntimeOperatorActivityOutcome.Cancelled,
            _ =>
                DesktopRuntimeOperatorActivityOutcome.Failed
        };

    private static DesktopRuntimeOperatorActivityOutcome GetActivityOutcome(
        DesktopRuntimePropertyWriteState state) =>
        state switch
        {
            DesktopRuntimePropertyWriteState.Succeeded =>
                DesktopRuntimeOperatorActivityOutcome.Succeeded,
            DesktopRuntimePropertyWriteState.Rejected =>
                DesktopRuntimeOperatorActivityOutcome.Rejected,
            DesktopRuntimePropertyWriteState.Cancelled =>
                DesktopRuntimeOperatorActivityOutcome.Cancelled,
            _ =>
                DesktopRuntimeOperatorActivityOutcome.Failed
        };
}
