using Hase.Client.Media;

namespace Hase.Client.Wpf.AppHost.Media;

public interface IClientMediaPresentationBoundary : IAsyncDisposable
{
    event Action<ClientMediaWebMessage>? ValidatedMessage;
    Task BeginAsync(bool includeAudio, CancellationToken cancellationToken = default);
    void SubmitNegotiation(RemoteMediaNegotiationMessage message);
    void ClearPresentation();
}
