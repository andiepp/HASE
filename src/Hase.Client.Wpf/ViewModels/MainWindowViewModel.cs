using Hase.Client.Wpf.Services;
using Prism.Commands;
using Prism.Mvvm;

namespace Hase.Client.Wpf.ViewModels;

/// <summary>
/// Projects normalized runtime-host client-session status for the application
/// shell without owning transport or physical endpoint lifecycles.
/// </summary>
public sealed class MainWindowViewModel
    : BindableBase
{
    private RuntimeHostClientSessionStatus sessionStatus =
        new(
            RuntimeHostClientSessionState.Disconnected);
    private RemoteObservationState currentState =
        RemoteObservationState.Empty;
    private IRuntimeHostClientSessionController? sessionController;
    private IClientConfigurationFilePicker? configurationFilePicker;
    private bool isBusy;
    private string? failureMessage;
    private RuntimeHostClientFailureCategory? lastFailureCategory;

    public MainWindowViewModel()
    {
        ConnectCommand =
            new DelegateCommand(
                ExecuteConnect,
                () =>
                    sessionController is not null
                    && !IsBusy
                    && CanConnect);
        DisconnectCommand =
            new DelegateCommand(
                ExecuteDisconnect,
                () =>
                    sessionController is not null
                    && !IsBusy
                    && CanDisconnect);
    }

    public string Title =>
        "HASE Laptop Client";

    public RuntimeHostClientSessionStatus SessionStatus =>
        sessionStatus;

    public string SessionState =>
        sessionStatus.State.ToString();

    public string RuntimeHostId =>
        sessionStatus.RuntimeHostId?.Value
        ?? "Not connected";

    public string ApiVersion =>
        sessionStatus.ApiVersion?.ToString()
        ?? "Not available";

    public bool CanConnect =>
        sessionStatus.State is
            RuntimeHostClientSessionState.Disconnected
            or RuntimeHostClientSessionState.Faulted;

    public bool CanDisconnect =>
        sessionStatus.State is
            RuntimeHostClientSessionState.Connecting
            or RuntimeHostClientSessionState.Connected
            or RuntimeHostClientSessionState.Reconnecting;

    public bool IsOperational =>
        sessionStatus.State
            == RuntimeHostClientSessionState.Connected;

    public bool IsStale =>
        sessionStatus.State
            == RuntimeHostClientSessionState.Reconnecting;

    public RemoteObservationState CurrentState =>
        currentState;

    public int EndpointCount =>
        currentState.Snapshot?.Attachments.Count
        ?? 0;

    public bool IsBusy
    {
        get =>
            isBusy;
        private set
        {
            if (SetProperty(
                    ref isBusy,
                    value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string? FailureMessage
    {
        get =>
            failureMessage;
        private set =>
            SetProperty(
                ref failureMessage,
                value);
    }

    public RuntimeHostClientFailureCategory? LastFailureCategory
    {
        get =>
            lastFailureCategory;
        private set =>
            SetProperty(
                ref lastFailureCategory,
                value);
    }

    public DelegateCommand ConnectCommand
    {
        get;
    }

    public DelegateCommand DisconnectCommand
    {
        get;
    }

    public void Configure(
        IRuntimeHostClientSessionController controller,
        IClientConfigurationFilePicker filePicker)
    {
        ArgumentNullException.ThrowIfNull(
            controller);
        ArgumentNullException.ThrowIfNull(
            filePicker);

        if (sessionController is not null)
        {
            throw new InvalidOperationException(
                "The main window client services are already configured.");
        }

        sessionController =
            controller;
        configurationFilePicker =
            filePicker;
        RaiseCommandStateChanged();
    }

    public void ApplySessionStatus(
        RuntimeHostClientSessionStatus value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        SetProperty(
            ref sessionStatus,
            value,
            nameof(SessionStatus));
        RaisePropertyChanged(
            nameof(SessionState));
        RaisePropertyChanged(
            nameof(RuntimeHostId));
        RaisePropertyChanged(
            nameof(ApiVersion));
        RaisePropertyChanged(
            nameof(CanConnect));
        RaisePropertyChanged(
            nameof(CanDisconnect));
        RaisePropertyChanged(
            nameof(IsOperational));
        RaisePropertyChanged(
            nameof(IsStale));
        RaiseCommandStateChanged();
    }

    public void ApplyObservationState(
        RemoteObservationState value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        SetProperty(
            ref currentState,
            value,
            nameof(CurrentState));
        RaisePropertyChanged(
            nameof(EndpointCount));
    }

    public async Task ConnectAsync()
    {
        if (sessionController is null
            || configurationFilePicker is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        string? configurationFilePath =
            configurationFilePicker.PickConfigurationFile();

        if (configurationFilePath is null)
        {
            return;
        }

        IsBusy =
            true;
        FailureMessage =
            null;
        LastFailureCategory =
            null;

        try
        {
            await sessionController.ConnectAsync(
                    configurationFilePath)
                .ConfigureAwait(
                    true);
        }
        catch
        {
            FailureMessage =
                "The runtime-host connection could not be started.";
        }
        finally
        {
            IsBusy =
                false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (sessionController is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        IsBusy =
            true;
        FailureMessage =
            null;

        try
        {
            await sessionController.DisconnectAsync()
                .ConfigureAwait(
                    true);
        }
        catch
        {
            FailureMessage =
                "The runtime-host connection could not be stopped cleanly.";
        }
        finally
        {
            IsBusy =
                false;
        }
    }

    private async void ExecuteConnect()
    {
        await ConnectAsync();
    }

    public void ApplySessionFailure(
        RuntimeHostClientFailureCategory category)
    {
        if (!Enum.IsDefined(
                category)
            || category == RuntimeHostClientFailureCategory.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category));
        }

        LastFailureCategory =
            category;
        FailureMessage =
            category switch
            {
                RuntimeHostClientFailureCategory.Authentication =>
                    "Runtime-host authentication failed.",
                RuntimeHostClientFailureCategory.Authorization =>
                    "Runtime-host access was denied.",
                RuntimeHostClientFailureCategory.ApiCompatibility =>
                    "The runtime-host API version is not supported.",
                RuntimeHostClientFailureCategory.LocalConfiguration =>
                    "The client configuration is invalid.",
                _ =>
                    "The runtime-host session ended unexpectedly."
            };
    }

    private async void ExecuteDisconnect()
    {
        await DisconnectAsync();
    }

    private void RaiseCommandStateChanged()
    {
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
    }
}
