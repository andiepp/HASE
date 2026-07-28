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

        WriteBooleanPropertyCommand =
            new DelegateCommand<DesktopRuntimePropertyViewModel>(
                ExecuteWriteBooleanProperty,
                property =>
                    property?.CanWriteRequestedValue
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

    public DelegateCommand<DesktopRuntimePropertyViewModel>
        WriteBooleanPropertyCommand
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
}
