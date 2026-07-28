using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimePropertyViewModel
    : INotifyPropertyChanged
{
    private static readonly TimeSpan ChangeHighlightDuration =
        TimeSpan.FromMilliseconds(1500);

    private readonly DispatcherTimer highlightTimer;

    private string displayName;
    private string path;
    private string access;
    private string value;
    private string quality;
    private string timestampUtc;
    private bool isKnown;
    private bool isRecentlyChanged;

    public DesktopRuntimePropertyViewModel(
        DesktopRuntimePropertySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        PropertyId =
            string.IsNullOrWhiteSpace(snapshot.PropertyId)
                ? throw new ArgumentException(
                    "The Property identity must not be empty.",
                    nameof(snapshot))
                : snapshot.PropertyId;
        displayName =
            snapshot.DisplayName;
        path =
            snapshot.Path;
        access =
            snapshot.Access;
        value =
            snapshot.Value;
        quality =
            snapshot.Quality;
        timestampUtc =
            snapshot.TimestampUtc;
        isKnown =
            snapshot.IsKnown;

        highlightTimer =
            new DispatcherTimer(
                DispatcherPriority.Background)
            {
                Interval =
                    ChangeHighlightDuration
            };
        highlightTimer.Tick +=
            OnHighlightTimerTick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PropertyId
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

    public string Path
    {
        get =>
            path;
        private set =>
            SetProperty(
                ref path,
                value);
    }

    public string Access
    {
        get =>
            access;
        private set =>
            SetProperty(
                ref access,
                value);
    }

    public string Value
    {
        get =>
            value;
        private set =>
            SetProperty(
                ref this.value,
                value);
    }

    public string Quality
    {
        get =>
            quality;
        private set =>
            SetProperty(
                ref quality,
                value);
    }

    public string TimestampUtc
    {
        get =>
            timestampUtc;
        private set =>
            SetProperty(
                ref timestampUtc,
                value);
    }

    public bool IsKnown
    {
        get =>
            isKnown;
        private set =>
            SetProperty(
                ref isKnown,
                value);
    }

    public bool IsRecentlyChanged
    {
        get =>
            isRecentlyChanged;
        private set =>
            SetProperty(
                ref isRecentlyChanged,
                value);
    }

    public void Update(
        DesktopRuntimePropertySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        if (!string.Equals(
                PropertyId,
                snapshot.PropertyId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A Property view model cannot be updated from a different "
                + "Property identity.",
                nameof(snapshot));
        }

        bool valueStateChanged =
            !string.Equals(
                Value,
                snapshot.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                Quality,
                snapshot.Quality,
                StringComparison.Ordinal)
            || IsKnown != snapshot.IsKnown;

        DisplayName =
            snapshot.DisplayName;
        Path =
            snapshot.Path;
        Access =
            snapshot.Access;
        Value =
            snapshot.Value;
        Quality =
            snapshot.Quality;
        TimestampUtc =
            snapshot.TimestampUtc;
        IsKnown =
            snapshot.IsKnown;

        if (valueStateChanged)
        {
            RestartChangeHighlight();
        }
    }

    private void RestartChangeHighlight()
    {
        highlightTimer.Stop();
        IsRecentlyChanged =
            true;
        highlightTimer.Start();
    }

    private void OnHighlightTimerTick(
        object? sender,
        EventArgs eventArgs)
    {
        highlightTimer.Stop();
        IsRecentlyChanged =
            false;
    }

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(
                field,
                value))
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
