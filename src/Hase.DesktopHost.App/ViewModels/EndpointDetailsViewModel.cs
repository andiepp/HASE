using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class EndpointDetailsViewModel
    : INotifyPropertyChanged,
      IDisposable
{
    private readonly RuntimeInventoryViewModel inventory;
    private DesktopRuntimeEndpointViewModel? observedEndpoint;
    private bool disposed;

    public EndpointDetailsViewModel(
        RuntimeInventoryViewModel inventory)
    {
        this.inventory =
            inventory
            ?? throw new ArgumentNullException(
                nameof(inventory));

        inventory.PropertyChanged +=
            OnInventoryPropertyChanged;

        ObserveEndpoint(
            inventory.SelectedEndpoint);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasSelection =>
        observedEndpoint is not null;

    public string DisplayName =>
        observedEndpoint?.DisplayName
        ?? string.Empty;

    public string EndpointId =>
        observedEndpoint?.EndpointId
        ?? string.Empty;

    public string ConnectionState =>
        observedEndpoint?.ConnectionState
        ?? string.Empty;

    public string StateIndicatorText =>
        observedEndpoint?.StateIndicatorText
        ?? string.Empty;

    public string AttachmentGeneration =>
        observedEndpoint?.AttachmentGeneration
        ?? string.Empty;

    public bool IsReady =>
        observedEndpoint?.IsReady
        ?? false;

    public bool IsRecovering =>
        observedEndpoint?.IsRecovering
        ?? false;

    public bool IsFaulted =>
        observedEndpoint?.IsFaulted
        ?? false;

    public bool IsDisconnected =>
        observedEndpoint?.IsDisconnected
        ?? false;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        inventory.PropertyChanged -=
            OnInventoryPropertyChanged;
        ObserveEndpoint(
            null);
    }

    private void OnInventoryPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (string.Equals(
                eventArgs.PropertyName,
                nameof(RuntimeInventoryViewModel.SelectedEndpoint),
                StringComparison.Ordinal)
            || string.IsNullOrEmpty(
                eventArgs.PropertyName))
        {
            ObserveEndpoint(
                inventory.SelectedEndpoint);
        }
    }

    private void ObserveEndpoint(
        DesktopRuntimeEndpointViewModel? endpoint)
    {
        if (ReferenceEquals(
                observedEndpoint,
                endpoint))
        {
            return;
        }

        if (observedEndpoint is not null)
        {
            observedEndpoint.PropertyChanged -=
                OnEndpointPropertyChanged;
        }

        observedEndpoint =
            endpoint;

        if (observedEndpoint is not null)
        {
            observedEndpoint.PropertyChanged +=
                OnEndpointPropertyChanged;
        }

        RaiseAllPropertiesChanged();
    }

    private void OnEndpointPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (string.IsNullOrEmpty(
                eventArgs.PropertyName))
        {
            RaiseAllPropertiesChanged();
            return;
        }

        OnPropertyChanged(
            eventArgs.PropertyName);
    }

    private void RaiseAllPropertiesChanged()
    {
        OnPropertyChanged(
            nameof(HasSelection));
        OnPropertyChanged(
            nameof(DisplayName));
        OnPropertyChanged(
            nameof(EndpointId));
        OnPropertyChanged(
            nameof(ConnectionState));
        OnPropertyChanged(
            nameof(StateIndicatorText));
        OnPropertyChanged(
            nameof(AttachmentGeneration));
        OnPropertyChanged(
            nameof(IsReady));
        OnPropertyChanged(
            nameof(IsRecovering));
        OnPropertyChanged(
            nameof(IsFaulted));
        OnPropertyChanged(
            nameof(IsDisconnected));
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}
