using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost;

public sealed class DesktopRuntimeHostViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IDesktopRuntimeHost runtimeHost;
    private bool disposed;

    public DesktopRuntimeHostViewModel(
        IDesktopRuntimeHost runtimeHost,
        DesktopRuntimeHostShellInformation shellInformation)
    {
        this.runtimeHost = runtimeHost ?? throw new ArgumentNullException(nameof(runtimeHost));
        ShellInformation = shellInformation
            ?? throw new ArgumentNullException(nameof(shellInformation));

        runtimeHost.StatusChanged += OnRuntimeHostStatusChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DesktopRuntimeHostShellInformation ShellInformation { get; }

    public DesktopRuntimeHostStatus Status => runtimeHost.Status;

    public string StatusText => Status.ToString();

    public string? ErrorMessage => runtimeHost.LastError?.Message;

    public bool HasError => runtimeHost.LastError is not null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return runtimeHost.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return runtimeHost.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        runtimeHost.StatusChanged -= OnRuntimeHostStatusChanged;
        disposed = true;
    }

    private void OnRuntimeHostStatusChanged(
        object? sender,
        DesktopRuntimeHostStatusChangedEventArgs eventArgs)
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
