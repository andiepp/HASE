using Hase.Runtime.Media;

namespace Hase.DesktopHost.App.Media;

public interface IRuntimeHostMediaWebBoundary :
    IRuntimeHostMediaCaptureBoundary,
    IAsyncDisposable
{
    event Action<RuntimeHostMediaWebMessage>? ValidatedMessage;
}
