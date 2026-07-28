using Prism.Commands;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class MainWindowViewModel : IDisposable
{
    private readonly DesktopRuntimeHostViewModel runtimeHostViewModel;
    private readonly IDesktopRuntimeHostOperator runtimeHostOperator;
    private bool disposed;

    public MainWindowViewModel(
        DesktopRuntimeHostViewModel runtimeHostViewModel,
        RuntimeInventoryViewModel inventoryViewModel,
        EndpointDetailsViewModel endpointDetailsViewModel,
        IDesktopRuntimeHostOperator runtimeHostOperator)
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
        Activity =
            new OperatorActivityViewModel();

        WriteBooleanPropertyCommand =
            new DelegateCommand<DesktopRuntimePropertyViewModel>(
                ExecuteWriteBooleanProperty,
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
    }

    public string ApplicationTitle =>
        "HASE Desktop Runtime Host";

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

    public DelegateCommand<DesktopRuntimePropertyViewModel>
        WriteBooleanPropertyCommand
    {
        get;
    }

    public DelegateCommand<DesktopRuntimeCommandViewModel>
        ExecuteParameterlessCommand
    {
        get;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        await runtimeHostViewModel.StartAsync(
            cancellationToken);

        Inventory.Refresh();
    }

    public Task StopAsync(
        CancellationToken cancellationToken = default) =>
        runtimeHostViewModel.StopAsync(
            cancellationToken);

    public void RefreshInventory()
    {
        Inventory.Refresh();
        WriteBooleanPropertyCommand.RaiseCanExecuteChanged();
        ExecuteParameterlessCommand.RaiseCanExecuteChanged();
    }

    public async Task ExecuteParameterlessCommandAsync(
        DesktopRuntimeCommandViewModel command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

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
        string reconciliation =
            string.Empty;

        try
        {
            Hase.Runtime.Northbound.RuntimeHostCommandOperationResult result =
                await runtimeHostOperator.ExecuteCommandAsync(
                    target,
                    argument: null,
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
                "None",
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

    public async Task WriteBooleanPropertyAsync(
        DesktopRuntimePropertyViewModel property,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            property);

        DesktopRuntimeBooleanPropertyWriteRequest? request =
            RuntimeHost.Status
                == DesktopRuntimeHostStatus.Running
                ? property.TryBeginBooleanWrite()
                : null;

        WriteBooleanPropertyCommand.RaiseCanExecuteChanged();

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
                DesktopRuntimeOperatorActivityKind.BooleanPropertyWrite,
                request.Target.EndpointId.Value,
                request.Target.AttachmentGeneration.ToString(),
                request.Target.InstrumentId.Value,
                operationPath,
                request.RequestedValue.ToString(),
                GetActivityOutcome(
                    property.WriteState),
                property.WriteState
                    == DesktopRuntimePropertyWriteState.Succeeded
                        ? string.Empty
                        : property.WriteMessage);
            WriteBooleanPropertyCommand.RaiseCanExecuteChanged();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        EndpointDetails.Dispose();
        runtimeHostViewModel.Dispose();
    }

    private async void ExecuteWriteBooleanProperty(
        DesktopRuntimePropertyViewModel property)
    {
        await WriteBooleanPropertyAsync(
            property,
            CancellationToken.None);
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
