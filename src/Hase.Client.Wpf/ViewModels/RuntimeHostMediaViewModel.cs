using Hase.Client;
using Hase.Client.Media;
using Prism.Commands;
using Prism.Mvvm;

namespace Hase.Client.Wpf.ViewModels;

/// <summary>
/// Presents explicit logical-camera selection and media lifecycle controls.
/// It never discovers local Client devices and never retains a Start across a
/// Runtime Host change or reconnect.
/// </summary>
public sealed class RuntimeHostMediaViewModel : BindableBase
{
    private IRuntimeHostMediaControlClient? client;
    private IReadOnlyList<RuntimeHostMediaSourceItemViewModel> sources = [];
    private RuntimeHostMediaSourceItemViewModel? selectedSource;
    private RemoteMediaSessionSnapshot? session;
    private bool includeAudio;
    private bool isBusy;
    private string statusText = "Media control is not available.";

    public RuntimeHostMediaViewModel()
    {
        RefreshCommand = new DelegateCommand(
            ExecuteRefresh,
            () => client is not null && !IsBusy && session is null);
        StartCommand = new DelegateCommand(
            ExecuteStart,
            () => client is not null && !IsBusy && session is null &&
                SelectedSource is { CanStart: true });
        StopCommand = new DelegateCommand(
            ExecuteStop,
            () => client is not null && !IsBusy && session is not null);
    }

    public IReadOnlyList<RuntimeHostMediaSourceItemViewModel> Sources
    {
        get => sources;
        private set => SetProperty(ref sources, value);
    }

    public RuntimeHostMediaSourceItemViewModel? SelectedSource
    {
        get => selectedSource;
        set
        {
            if (session is not null || !SetProperty(ref selectedSource, value))
            {
                return;
            }

            if (value is not { SupportsAudio: true })
            {
                IncludeAudio = false;
            }
            RaiseStateChanged();
        }
    }

    public bool IncludeAudio
    {
        get => includeAudio;
        set
        {
            bool normalized = value && SelectedSource is { SupportsAudio: true };
            if (SetProperty(ref includeAudio, normalized))
            {
                RaiseStateChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                RaiseStateChanged();
            }
        }
    }

    public bool CanSelectSource => session is null && !IsBusy;
    public bool HasSources => Sources.Count > 0;
    public bool CanRequestAudio =>
        SelectedSource is { SupportsAudio: true } && session is null && !IsBusy;
    public string SessionState => session?.State.ToString() ?? "Idle";

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand StartCommand { get; }
    public DelegateCommand StopCommand { get; }

    public void Configure(IRuntimeHostMediaControlClient mediaClient)
    {
        ArgumentNullException.ThrowIfNull(mediaClient);
        if (client is not null)
        {
            throw new InvalidOperationException(
                "The media control client is already configured.");
        }

        client = mediaClient;
        if (mediaClient is IRuntimeHostMediaSessionNotifications notifications)
        {
            notifications.SessionChanged += OnSessionChanged;
        }
        StatusText = "Select Refresh Cameras to load configured sources.";
        RaiseStateChanged();
    }

    public void ResetForRuntimeHostChange()
    {
        session = null;
        Sources = [];
        SelectedSource = null;
        IncludeAudio = false;
        StatusText = client is null
            ? "Media control is not available."
            : "Refresh cameras for the selected Runtime Host.";
        RaiseStateChanged();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IRuntimeHostMediaControlClient activeClient = client ??
            throw new InvalidOperationException("Media control is not configured.");
        if (session is not null)
        {
            throw new InvalidOperationException(
                "Camera capabilities cannot be refreshed during a media session.");
        }

        IsBusy = true;
        try
        {
            IReadOnlyList<RemoteMediaSourceCapability> capabilities =
                await activeClient.GetCapabilitiesAsync(cancellationToken);
            Sources = capabilities
                .Where(item => item.SupportsVideo)
                .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
                .ThenBy(item => item.Target.MediaSourceId, StringComparer.Ordinal)
                .Select(item => new RuntimeHostMediaSourceItemViewModel(
                    item.Target,
                    item.DisplayName,
                    item.Availability,
                    item.SupportsAudio))
                .ToArray();
            SelectedSource = Sources.Count == 1 ? Sources[0] : null;
            StatusText = Sources.Count == 0
                ? "No configured cameras are available."
                : "Select a camera and choose Start Video.";
        }
        catch (RuntimeHostClientException exception)
        {
            Sources = [];
            SelectedSource = null;
            StatusText = $"Camera capabilities failed ({exception.Category}): "
                + exception.Message;
        }
        catch
        {
            Sources = [];
            SelectedSource = null;
            StatusText = "Camera capabilities could not be loaded.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        IRuntimeHostMediaControlClient activeClient = client ??
            throw new InvalidOperationException("Media control is not configured.");
        RuntimeHostMediaSourceItemViewModel source = SelectedSource ??
            throw new InvalidOperationException("A logical camera must be selected.");
        if (!source.CanStart || session is not null)
        {
            throw new InvalidOperationException("The selected camera cannot be started.");
        }

        IsBusy = true;
        StatusText = "Starting selected camera...";
        try
        {
            RemoteMediaStartResult result = await activeClient.StartAsync(
                source.Target,
                IncludeAudio,
                cancellationToken);
            if (!result.Succeeded || result.Session is null)
            {
                StatusText = MapFailure(result.FailureCode);
                return;
            }

            session = result.Session;
            StatusText = "Encrypted media negotiation started.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Starting the camera was canceled.";
        }
        catch
        {
            StatusText = "The media operation failed.";
        }
        finally
        {
            IsBusy = false;
            RaiseStateChanged();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        IRuntimeHostMediaControlClient activeClient = client ??
            throw new InvalidOperationException("Media control is not configured.");
        RemoteMediaSessionSnapshot activeSession = session ??
            throw new InvalidOperationException("There is no active media session.");

        IsBusy = true;
        StatusText = "Stopping media session...";
        try
        {
            RemoteMediaStopResult result = await activeClient.StopAsync(
                activeSession.SessionId,
                cancellationToken);
            if (!result.Succeeded)
            {
                StatusText = MapFailure(result.FailureCode);
                return;
            }

            session = null;
            StatusText = "Media session stopped.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Stopping the media session was canceled.";
        }
        catch
        {
            StatusText = "The media operation failed.";
        }
        finally
        {
            IsBusy = false;
            RaiseStateChanged();
        }
    }

    private static string MapFailure(string? failureCode) => failureCode switch
    {
        "source-not-current" => "The camera selection is stale. Refresh cameras.",
        "source-unavailable" => "The selected camera is unavailable.",
        "session-busy" => "Another camera session is already active.",
        "audio-not-supported" => "Audio is unavailable for the selected camera.",
        _ => "The media operation failed."
    };

    private void OnSessionChanged(
        object? sender,
        RemoteMediaSessionChangedEventArgs eventArgs)
    {
        session = eventArgs.Session;
        StatusText = eventArgs.StatusText;
        if (session is null)
        {
            IncludeAudio = false;
        }
        RaiseStateChanged();
    }

    private async void ExecuteRefresh() => await RefreshAsync();
    private async void ExecuteStart() => await StartAsync();
    private async void ExecuteStop() => await StopAsync();

    private void RaiseStateChanged()
    {
        RaisePropertyChanged(nameof(CanSelectSource));
        RaisePropertyChanged(nameof(HasSources));
        RaisePropertyChanged(nameof(CanRequestAudio));
        RaisePropertyChanged(nameof(SessionState));
        RefreshCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}
