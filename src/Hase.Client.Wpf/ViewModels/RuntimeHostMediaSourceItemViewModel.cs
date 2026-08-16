using Hase.Client.Media;

namespace Hase.Client.Wpf.ViewModels;

public sealed record RuntimeHostMediaSourceItemViewModel(
    RemoteMediaSourceTarget Target,
    string DisplayName,
    RemoteMediaSourceAvailability Availability,
    bool SupportsAudio)
{
    public bool CanStart => Availability == RemoteMediaSourceAvailability.Idle;
}
