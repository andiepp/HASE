using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimeEndpointViewModel
    : INotifyPropertyChanged
{
    private string displayName;
    private string connectionState;
    private string attachmentGeneration;

    public DesktopRuntimeEndpointViewModel(
        string endpointId,
        string displayName,
        string connectionState,
        string attachmentGeneration)
    {
        EndpointId =
            string.IsNullOrWhiteSpace(endpointId)
                ? throw new ArgumentException(
                    "The endpoint identity must not be empty.",
                    nameof(endpointId))
                : endpointId;
        this.displayName =
            string.IsNullOrWhiteSpace(displayName)
                ? endpointId
                : displayName;
        this.connectionState =
            string.IsNullOrWhiteSpace(connectionState)
                ? "Unknown"
                : connectionState;
        this.attachmentGeneration =
            string.IsNullOrWhiteSpace(attachmentGeneration)
                ? throw new ArgumentException(
                    "The attachment generation must not be empty.",
                    nameof(attachmentGeneration))
                : attachmentGeneration;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string EndpointId
    {
        get;
    }

    public string DisplayName
    {
        get =>
            displayName;
        private set =>
            SetProperty(
                ref displayName,
                value);
    }

    public string ConnectionState
    {
        get =>
            connectionState;
        private set =>
            SetProperty(
                ref connectionState,
                value);
    }

    public string AttachmentGeneration
    {
        get =>
            attachmentGeneration;
        private set =>
            SetProperty(
                ref attachmentGeneration,
                value);
    }

    public bool IsReady =>
        IsState(
            "Ready");

    public bool IsRecovering =>
        IsState(
            "Connecting")
        || IsState(
            "Synchronizing")
        || IsState(
            "Reconnecting");

    public bool IsFaulted =>
        IsState(
            "Faulted");

    public bool IsDisconnected =>
        IsState(
            "Disconnected");

    public string StateIndicatorText =>
        IsReady
            ? "● Ready"
            : IsRecovering
                ? "◐ " + ConnectionState
                : IsFaulted
                    ? "⚠ Faulted"
                    : IsDisconnected
                        ? "○ Disconnected"
                        : "• " + ConnectionState;

    public void Update(
        DesktopRuntimeEndpointSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        if (!string.Equals(
                EndpointId,
                snapshot.EndpointId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "An endpoint view model cannot be updated from a different "
                + "endpoint identity.",
                nameof(snapshot));
        }

        DisplayName =
            string.IsNullOrWhiteSpace(snapshot.DisplayName)
                ? snapshot.EndpointId
                : snapshot.DisplayName;
        AttachmentGeneration =
            snapshot.AttachmentGeneration;

        if (!string.Equals(
                ConnectionState,
                snapshot.ConnectionState,
                StringComparison.Ordinal))
        {
            ConnectionState =
                snapshot.ConnectionState;
            RaiseStatePropertiesChanged();
        }
    }

    private bool IsState(
        string state) =>
        string.Equals(
            ConnectionState,
            state,
            StringComparison.Ordinal);

    private void RaiseStatePropertiesChanged()
    {
        OnPropertyChanged(
            nameof(IsReady));
        OnPropertyChanged(
            nameof(IsRecovering));
        OnPropertyChanged(
            nameof(IsFaulted));
        OnPropertyChanged(
            nameof(IsDisconnected));
        OnPropertyChanged(
            nameof(StateIndicatorText));
    }

    private bool SetProperty(
        ref string field,
        string value,
        [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(
                field,
                value,
                StringComparison.Ordinal))
        {
            return false;
        }

        field =
            value;
        OnPropertyChanged(
            propertyName);
        return true;
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
