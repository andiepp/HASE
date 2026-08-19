using Hase.Runtime.Media;

namespace Hase.DesktopHost.App.Media;

public interface IRuntimeHostMediaInventoryWebBoundary : IAsyncDisposable
{
    event Action<IReadOnlyList<RuntimeHostMediaDeviceObservation>>?
        InventoryChanged;

    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
}
